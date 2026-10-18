using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CompositionHostTests
{
    [TestCase(typeof(RetryOwner), 3)]
    [TestCase(typeof(ClassRetryOwner), 2)]
    [TestCase(typeof(MethodRetryOwner), 1)]
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
            Assert.That(execution.IsCompleted, Is.False);
            owner.Ready.SetResult();
            var scenario = await execution;
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.RetryCount, Is.EqualTo(retries));
            Assert.That(owner.Executions, Is.EqualTo(retries + 1));
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
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.RetryCount, Is.EqualTo(1));
            Assert.That(owner.Host.Context, Is.Not.SameAs(original));
            Assert.That(owner.Host.Context.Value, Is.EqualTo("paid"));
            Assert.That(owner.Hooks, Is.EqualTo(new[] { "base teardown", "owner teardown", "base setup", "owner setup" }));
            Assert.That(scenario.GetStoryText(), Does.Contain("shopper"));
            Assert.That(scenario.GetScenarioText(), Is.EqualTo("The shopper pays"));
            Assert.That(scenario.Tags, Is.EquivalentTo(new[] { "checkout", "payment" }));
            Assert.That(Required(scenario.CustomTestInformation).Single().Information, Is.EqualTo("payment reference"));
            Assert.That(Required(scenario.RuntimeInformation).CallerMember, Is.EqualTo(nameof(Owner.Pays)));
            Assert.That(scenario.FileName, Does.EndWith("CompositionHostTests.cs"));
        }
        finally { owner.Host.EndTest(); }
    }

    [TestCase(false)]
    [TestCase(true)]
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
                    Assert.That(host.Context.Value, Is.EqualTo(label));
                }).Then(() => Complete()).BDTestAsync();
                Console.WriteLine(label + " teardown");
                return scenario;
            }
            finally
            {
                var context = host.Context;
                end();
                end();
                Assert.That(host.Context, Is.Not.SameAs(context));
                Assert.That(run.OutputBuffers, Is.Empty);
                Assert.That(TestOutputData.FrameworkExecutionId, Is.Null);
            }
        }

        var scenarios = await Task.WhenAll(Task.Run(() => Execute(first, "first")), Task.Run(() => Execute(second, "second")));
        foreach (var (run, label, other, index) in new[] { (first, "first", "second", 0), (second, "second", "first", 1) })
        {
            var scenario = scenarios[index];
            Assert.That(run.Scenarios, Is.EqualTo(new[] { scenario }));
            Assert.That(scenario.FrameworkTestId, Is.EqualTo("same-id"));
            Assert.That(scenario.ReportId, Is.EqualTo(run.ReportId));
            Assert.That(scenario.TestStartupInformation, Does.Contain(label + " setup").And.Not.Contain(other));
            Assert.That(scenario.Output, Does.Contain(label + " step").And.Not.Contain(other));
            Assert.That(scenario.TearDownOutput, Does.Contain(label + " teardown").And.Not.Contain(other));
            Assert.That(scenario.CustomHtmlOutputForReport, Does.Contain(label + " html").And.Not.Contain(other));
            var report = Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run)));
            Assert.That(Required(report.Scenarios).Single().ReportId, Is.EqualTo(run.ReportId));
            Assert.That(report.Scenarios.Single().FrameworkTestId, Is.EqualTo("same-id"));
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
            await Assert.ThrowsAsync<SkipException>(async () => await owner.Host.When(() => ThrowSkip()).Then(() => Complete()).BDTestAsync());
            Assert.That(run.Scenarios.Single().Status, Is.EqualTo(Status.Skipped));
        }
        finally { owner.Host.EndTest(); }
        owner.Host.BeginTest("whole-skip");
        try
        {
            var skipped = await owner.Host.When(() => ThrowSkip()).Then(() => Complete())
                .WithScenarioText("rule skip").BDTestAsync();
            Assert.That(skipped.Status, Is.EqualTo(Status.Skipped));
            Assert.That(skipReason, Is.Not.Null.And.Not.Empty);
        }
        finally { owner.Host.EndTest(); }
        owner.Host.BeginTest("outline", typeof(Owner).GetMethod(nameof(Owner.Examples)));
        try
        {
            var outline = await owner.Examples();
            Assert.That(outline.Tags, Is.EquivalentTo(new[] { "checkout", "examples" }));
            Assert.That(outline.GetStoryText(), Does.Contain("shopper"));
            Assert.That(Required(outline.Examples).Select(row => row.Status), Is.All.EqualTo(Status.Passed));
            Assert.That(outline.Examples, Has.Count.EqualTo(2));
            Assert.That(owner.Contexts.Distinct().Count(), Is.EqualTo(2));
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
                Assert.That(scenario.Tags, Is.EqualTo(new[] { "checkout" }));
                Assert.That(scenario.CustomTestInformation, Is.Empty);
                Assert.That(scenario.GetScenarioText(),
                    Is.EqualTo("Without method identity composition uses owner defaults and caller text"));
                Assert.That(scenario.GetStoryText(), Does.Contain("shopper"));
            }
            finally { owner.Host.EndTest(); }
        }
    }

    [Test]
    public void Invalid_owner_and_disposed_run_are_rejected()
    {
        using var run = BDTestRun.Start();
        Assert.Throws<ArgumentNullException>(() => BDTestScenario.For<State>(null!, run));
        run.Dispose();
        Assert.Throws<ObjectDisposedException>(() => BDTestScenario.For<State>(this, run));
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
