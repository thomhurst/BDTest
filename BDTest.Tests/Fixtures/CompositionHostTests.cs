using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class CompositionHostTests
{
    [Test]
    [Arguments(typeof(RetryOwner), 3)]
    [Arguments(typeof(ClassRetryOwner), 2)]
    [Arguments(typeof(MethodRetryOwner), 1)]
    public async Task Owner_retry_attributes_apply_after_the_owner_method_returns(Type ownerType, int retries)
    {
        // Isolate assembly defaults so other tests do not inherit this retry policy.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CompositionRetryTests_{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BDTestRetryAttribute).GetConstructor(new[] { typeof(int) })!, new object[] { 3 }));
        var type = assembly.DefineDynamicModule("Tests")
            .DefineType("Owner", TypeAttributes.Public, ownerType).CreateType()!;
        var owner = (RetryOwner)Activator.CreateInstance(type)!;
        using var run = BDTestRun.Start();
        owner.Host = BDTestScenario.For<State>(owner, run);
        owner.Failures = retries;
        owner.Host.BeginTest("attributed-retry", ownerType.GetMethod(nameof(RetryOwner.Run)));
        try
        {
            var execution = owner.Run();
            await Assert.That(execution.IsCompleted).IsFalse();
            owner.Ready.SetResult();
            var scenario = await execution;
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
            await Assert.That(scenario.RetryCount).IsEqualTo(retries);
            await Assert.That(owner.Executions).IsEqualTo(retries + 1);
        }
        finally { owner.Host.EndTest(); }
    }

    [Test]
    public async Task Owner_metadata_and_private_inherited_retry_hooks_use_the_test_instance()
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(exception => exception is TransientException, 1);
        using var run = BDTestRun.Start(options);
        var owner = new Owner(run);
        owner.Host.BeginTest("retry", typeof(Owner).GetMethod(nameof(Owner.Pays)));
        try
        {
            var original = owner.Host.Context;
            var scenario = await owner.Pays();
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
            await Assert.That(scenario.RetryCount).IsEqualTo(1);
            await Assert.That(owner.Host.Context).IsNotSameReferenceAs(original);
            await Assert.That(owner.Host.Context.Value).IsEqualTo("paid");
            await Assert.That(owner.Hooks).IsEquivalentTo(new[] { "base teardown", "owner teardown", "base setup", "owner setup" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(scenario.GetStoryText()).Contains("shopper");
            await Assert.That(scenario.GetScenarioText()).IsEqualTo("The shopper pays");

            await Assert.That(scenario.Tags).IsEquivalentTo(new[] { "checkout", "payment" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
            await Assert.That(Required(scenario.CustomTestInformation).Single().Information).IsEqualTo("payment reference");
            await Assert.That(Required(scenario.RuntimeInformation).CallerMember).IsEqualTo(nameof(Owner.Pays));
            await Assert.That(scenario.FileName).EndsWith("CompositionHostTests.cs");
        }
        finally { owner.Host.EndTest(); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Explicit_runs_isolate_context_output_reports_and_completion(bool composition)
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Scenario> Execute(BDTestRun run, string label)
        {
            AbstractContextBDTestBase<State> host;
            Action end;
            if (composition)
            {
                var composed = BDTestScenario.For<State>(new object(), run);
                composed.BeginTest("same-id");
                host = composed;
                end = composed.EndTest;
            }
            else
            {
                var inherited = new InheritedHost(run);
                inherited.Start("same-id");
                host = inherited;
                end = inherited.Finish;
            }
            try
            {
                host.Context.Value = label;
                Console.WriteLine(label + " setup");
                host.ScenarioHtmlWriter.Text(label + " html");
                var scenario = await host.When(async () =>
                {
                    Console.WriteLine(label + " step");
                    if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await Assert.That(host.Context.Value).IsEqualTo(label);
                }).Then(() => Complete()).BDTestAsync();
                Console.WriteLine(label + " teardown");
                return scenario;
            }
            finally
            {
                var context = host.Context;
                end();
                end();
                await Assert.That(host.Context).IsNotSameReferenceAs(context);
                await Assert.That(run.OutputBuffers).IsEmpty();
                await Assert.That(TestOutputData.FrameworkExecutionId).IsNull();
            }
        }

        var scenarios = await Task.WhenAll(Task.Run(() => Execute(first, "first")), Task.Run(() => Execute(second, "second")));
        foreach (var (run, label, other, index) in new[] { (first, "first", "second", 0), (second, "second", "first", 1) })
        {
            var scenario = scenarios[index];
            await Assert.That(run.Scenarios).IsEquivalentTo(new[] { scenario }, EqualityComparer<Scenario>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(scenario.FrameworkTestId).IsEqualTo("same-id");
            await Assert.That(scenario.ReportId).IsEqualTo(run.ReportId);

            await Assert.That(scenario.TestStartupInformation).Contains(label + " setup").And.DoesNotContain(other);

            await Assert.That(scenario.Output).Contains(label + " step").And.DoesNotContain(other);

            await Assert.That(scenario.TearDownOutput).Contains(label + " teardown").And.DoesNotContain(other);

            await Assert.That(scenario.CustomHtmlOutputForReport).Contains(label + " html").And.DoesNotContain(other);
            var report = Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run)));
            await Assert.That(Required(report.Scenarios).Single().ReportId).IsEqualTo(run.ReportId);
            await Assert.That(report.Scenarios.Single().FrameworkTestId).IsEqualTo("same-id");
        }
    }

    [Test]
    public async Task Outcome_mapping_and_outline_metadata_work_without_inheritance()
    {
        string? skipReason = null;
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(new FrameworkOutcomeMap(reason => skipReason = reason).Skip<SkipException>());
        options.SkipTestRules.Add(scenario => scenario.GetScenarioText() == "rule skip");
        using var run = BDTestRun.Start(options);
        var owner = new Owner(run);
        owner.Host.BeginTest("skip");
        try
        {
            await Assert.That(async () => await owner.Host.When(() => ThrowSkip()).Then(() => Complete()).BDTestAsync()).ThrowsExactly<SkipException>();
            await Assert.That(run.Scenarios.Single().Status).IsEqualTo(Status.Skipped);
        }
        finally { owner.Host.EndTest(); }
        owner.Host.BeginTest("whole-skip");
        try
        {
            var skipped = await owner.Host.When(() => ThrowSkip()).Then(() => Complete())
                .WithScenarioText("rule skip").BDTestAsync();
            await Assert.That(skipped.Status).IsEqualTo(Status.Skipped);

            await Assert.That(skipReason).IsNotNull().And.IsNotEmpty();
        }
        finally { owner.Host.EndTest(); }
        owner.Host.BeginTest("outline", typeof(Owner).GetMethod(nameof(Owner.Examples)));
        try
        {
            var outline = await owner.Examples();

            await Assert.That(outline.Tags).IsEquivalentTo(new[] { "checkout", "examples" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
            await Assert.That(outline.GetStoryText()).Contains("shopper");

            await Assert.That((Required(outline.Examples).Select(row => row.Status)).All(item => Equals(item, Status.Passed))).IsTrue();
            await Assert.That(outline.Examples).Count().IsEqualTo(2);
            await Assert.That(owner.Contexts.Distinct().Count()).IsEqualTo(2);
        }
        finally { owner.Host.EndTest(); }
    }

    [Test]
    public async Task Without_method_identity_composition_uses_owner_defaults_and_caller_text()
    {
        using var run = BDTestRun.Start();
        var owner = new Owner(run);
        foreach (var beginExecution in new[] { false, true })
        {
            if (beginExecution) owner.Host.BeginTest("fallback");
            try
            {
                var scenario = await owner.Host.When(() => Complete()).Then(() => Complete()).BDTestAsync();
                await Assert.That(scenario.Tags).IsEquivalentTo(new[] { "checkout" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
                await Assert.That(scenario.CustomTestInformation).IsEmpty();
                await Assert.That(scenario.GetScenarioText()).IsEqualTo("Without method identity composition uses owner defaults and caller text");
                await Assert.That(scenario.GetStoryText()).Contains("shopper");
            }
            finally { owner.Host.EndTest(); }
        }
    }

    [Test]
    public async Task Invalid_owner_and_disposed_run_are_rejected()
    {
        using var run = BDTestRun.Start();
        await Assert.That(() => BDTestScenario.For<State>(null!, run)).ThrowsExactly<ArgumentNullException>();
        run.Dispose();
        await Assert.That(() => BDTestScenario.For<State>(this, run)).ThrowsExactly<ObjectDisposedException>();
    }

    private static void Complete() { }
    private static void ThrowSkip() => throw new SkipException();
    private sealed class SkipException : Exception { }
    private sealed class TransientException : Exception { }
    public sealed class State { public string? Value { get; set; } }

    public class CompanyBase
    {
        public List<string> Hooks { get; } = new();
        [BDTestRetryTearDown] private void TearDown() => Hooks.Add("base teardown");
        [BDTestRetrySetUp] private Task SetUp() { Hooks.Add("base setup"); return Task.CompletedTask; }
    }

    [Story(AsA = "shopper", IWant = "to pay", SoThat = "I receive my order")]
    [BDTestTag("checkout")]
    private sealed class Owner : CompanyBase
    {
        public BDTestScenario<State> Host { get; }
        public List<State> Contexts { get; } = new();
        private int _attempts;
        public Owner(BDTestRun run) => Host = BDTestScenario.For<State>(this, run);

        [ScenarioText("The shopper pays"), BDTestTag("payment"), TestInformation("payment reference")]
        public Task<Scenario> Pays() => Host.Given(() => Complete()).When(() => Pay()).Then(() => Complete()).BDTestAsync();

        private void Pay()
        {
            if (++_attempts == 1) throw new TransientException();
            Host.Context.Value = "paid";
        }

        [BDTestTag("examples")]
        public Task<Scenario> Examples() => Host.ScenarioOutline("example <Value>", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Host.When(() => Remember(row.Value)).Then(() => Complete())).BDTestAsync();

        private void Remember(int value) { Host.Context.Value = value.ToString(); Contexts.Add(Host.Context); }
        [BDTestRetryTearDown] private void RetryTearDown() => Hooks.Add("owner teardown");
        [BDTestRetrySetUp] private void RetrySetUp() => Hooks.Add("owner setup");
    }

    public class RetryOwner
    {
        public BDTestScenario<State> Host { get; set; } = null!; // Assigned before Run by each test.
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Failures { get; set; }
        public int Executions { get; private set; }

        public virtual Task<Scenario> Run() => Host.When(() => Execute()).Then(() => Complete()).BDTestAsync();

        private async Task Execute()
        {
            // The test releases this gate only after Run has returned to its caller.
            await Ready.Task;
            if (++Executions <= Failures) throw new TransientException();
        }
    }

    [BDTestRetry(2)]
    public class ClassRetryOwner : RetryOwner { }

    public class MethodRetryOwner : ClassRetryOwner
    {
        [BDTestRetry(1)]
        public override Task<Scenario> Run() => base.Run();
    }

    private sealed class InheritedHost(BDTestRun run) : AbstractContextBDTestBase<State>
    {
        public override BDTestRun TestRun => run;
        public void Start(string id) => BeginTest(id);
        public void Finish() => EndTest();
    }
}
