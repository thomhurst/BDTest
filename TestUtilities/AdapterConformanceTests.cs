using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;
#if BDTEST_NUNIT
using Adapter = BDTest.NUnit.NUnitBDTestBase<object>;
using TestAttribute = global::NUnit.Framework.TestAttribute;
#elif BDTEST_MSTEST
using Adapter = BDTest.MSTest.MSTestBDTestBase<object>;
using TestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#elif BDTEST_TUNIT
using Adapter = BDTest.TUnit.TUnitBDTestBase<object>;
using TestAttribute = global::TUnit.Core.TestAttribute;
#elif BDTEST_XUNIT
using Adapter = BDTest.xUnit.XUnitBDTestBase<object>;
using TestAttribute = Xunit.FactAttribute;
#endif

namespace BDTest.Adapters.Conformance;

// Compile the same contract into every adapter project. The runner script enables
// intentional failures and checks TRX; ordinary test runs catch those failures.
#if BDTEST_NUNIT
[global::NUnit.Framework.FixtureLifeCycle(global::NUnit.Framework.LifeCycle.InstancePerTestCase)]
#elif BDTEST_MSTEST
[global::BDTest.MSTest.BDTestClass]
#endif
public class AdapterConformanceTests : Adapter
{
    private readonly List<(string Name, int Attempt, object Context, CancellationToken Token, Status Status)> _hooks = new();
    private readonly BDTestRun _run = CreateRun();
    public override BDTestRun TestRun => _run;

    private static BDTestRun CreateRun()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(OutcomeMap);
        options.RegisterFrameworkOutcomes(OutcomeMap);
        options.SkipStepRules.Add<SkippedAttribute>(_ => true);
        options.SkipTestRules.Add(s => s.GetScenarioText() == "Conformance global skip");
        options.RetryTestRules.Add(e => e is TransientException, 1);
        options.ScenarioHooks.Add(new ScenarioHook());
        return BDTestRun.Start(options);
    }

    public override Task OnBeforeScenario(ScenarioHookContext<object> scenario) => Record("instance before", scenario);
    public override Task OnAfterScenario(ScenarioHookContext<object> scenario) => Record("instance after", scenario);

    private Task Record(string name, ScenarioHookContext scenario)
    {
        Require(ReferenceEquals(Context, scenario.Context), "scenario hook received another attempt's context");
        _hooks.Add((name, scenario.Attempt, scenario.Context, scenario.CancellationToken, scenario.Scenario.Status));
        if (name.EndsWith("after") && scenario.Scenario.Status is Status.Failed or Status.NotImplemented)
            Require(scenario.Scenario.Exception != null, "after hook did not receive the attempt exception");
        return Task.CompletedTask;
    }

    private sealed class ScenarioHook : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) =>
            ((AdapterConformanceTests)scenario.Scenario.BdTestBaseClass).Record("run before", scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) =>
            ((AdapterConformanceTests)scenario.Scenario.BdTestBaseClass).Record("run after", scenario);
    }

    [Test]
    public Task Pass() => Check(() => Given(() => Complete()).When(() => Complete())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Passed, Status.Passed);

    [Test]
    public Task Fail() => CheckFailure(() => Check(() => Given(() => Complete()).When(() => FailStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Failed, 0, Status.Passed, Status.Failed, Status.NotRun),
        "conformance failure");

    [Test]
    public Task NotImplemented() => CheckFailure(() => Check(() => Given(() => Complete()).When(() => NotImplementedStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.NotImplemented, 0,
        Status.Passed, Status.NotImplemented, Status.NotRun), "conformance not implemented");

    [Test]
    public Task SingleStepSkipped() => Check(() => Given(() => Complete()).When(() => SkippedStep())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Skipped, Status.Passed);

    [Test]
    public Task AllStepsSkipped() => Check(() => Given(() => SkippedStep()).When(() => SkippedStep())
        .Then(() => SkippedStep()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkips() => Check(() => Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkipsWithDirectAwait() => Check(async () => await Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test]
    public Task FrameworkSkip() => Check(() => Given(() => Complete()).When(() => OutcomeMap.ReportSkip("conformance skip"))
        .Then(() => MustNotRun()).BDTestAsync(), FrameworkSkipStatus, 0,
        Status.Passed, FrameworkSkipStatus, FrameworkSkipStatus == Status.Skipped ? Status.Skipped : Status.NotRun);

    [Test]
    public Task FrameworkInconclusive()
    {
        Task Execute() => Check(() => Given(() => Complete())
            .When(() => OutcomeMap.ReportInconclusive("conformance inconclusive"))
            .Then(() => MustNotRun()).BDTestAsync(), FrameworkInconclusiveStatus, 0,
            Status.Passed, FrameworkInconclusiveStatus, FrameworkInconclusiveStatus == Status.Skipped ? Status.Skipped : Status.NotRun);
#if BDTEST_TUNIT
        // TUnit 1.75.0 reports its InconclusiveTestException as Failed in TRX.
        return CheckFailure(Execute, "conformance inconclusive");
#else
        return Execute();
#endif
    }

    [Test]
    public Task FrameworkSuccess() => Check(() => Given(() => Complete()).When(() => ReportSuccess())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0,
        Status.Passed, Status.Passed, HasSuccessException ? Status.NotRun : Status.Passed);

    [Test]
    public Task RetryThenPass()
    {
        var attempts = 0;
        void FailOnce() { if (++attempts == 1) throw new TransientException(); }
        return Check(() => Given(() => Complete()).When(() => FailOnce()).Then(() => Complete()).BDTestAsync(),
            Status.Passed, 1, Status.Passed, Status.Passed, Status.Passed);
    }

    [Test]
    public async Task ScenarioTimeoutHooks()
    {
        try
        {
            await Check(() => When(async token => await Task.Delay(Timeout.InfiniteTimeSpan, token))
                .Then(() => MustNotRun()).WithScenarioTimeout(TimeSpan.FromMilliseconds(100)).BDTestAsync(),
                Status.Failed, 0, Status.Failed, Status.NotRun);
        }
        catch (TimeoutException)
        {
            Require(_hooks.Where(h => h.Name.EndsWith("after")).All(h => h.Token.IsCancellationRequested),
                "timeout did not cancel the scenario hook token");
            return;
        }
        throw new InvalidOperationException("Expected timeout");
    }

    [Test]
    public async Task ScenarioOutlineHooks()
    {
        var outline = await ScenarioOutline("example <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
        {
            var calls = 0;
            void FailOnce() { if (++calls == 1) throw new TransientException(); }
            return When(() => FailOnce()).Then(() => Complete());
        }).BDTestAsync();
        Require(outline.Examples.All(s => s.Status == Status.Passed && s.RetryCount == 1), "outline retry mismatch");
        Require(_hooks.Count == 16, "hooks must run for each row attempt, not the outline container");
        Require(_hooks.Where(h => h.Name == "run before").Select(h => h.Context).Distinct().Count() == 4,
            "outline attempts reused contexts");
        Require(_hooks.Where(h => h.Name == "run before").Select(h => h.Token).Distinct().Count() == 4,
            "outline attempts reused cancellation tokens");
        Require(_hooks.Where(h => h.Name == "instance after").Select(h => h.Status)
            .SequenceEqual(new[] { Status.Failed, Status.Passed, Status.Failed, Status.Passed }), "outline hook outcomes mismatch");
    }

    [Test]
    public async Task ExplicitRunsUseDifferentMappings()
    {
        var skipMap = new FrameworkOutcomeMap(_ => { }).Skip<SharedOutcomeException>();
        var firstOptions = new BDTestOptions();
        firstOptions.RegisterFrameworkOutcomes(skipMap);
        firstOptions.RegisterFrameworkOutcomes(skipMap);
        Require(firstOptions.SkipExceptionTypes.Count == 1, "registration must be idempotent");
        using var first = BDTestRun.Start(firstOptions);
        var secondOptions = new BDTestOptions();
        secondOptions.RegisterFrameworkOutcomes(new FrameworkOutcomeMap(_ => { }).Inconclusive<SharedOutcomeException>());
        using var second = BDTestRun.Start(secondOptions);
        firstOptions.RegisterFrameworkOutcomes(skipMap.Success<SharedOutcomeException>());
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ThrowTogether()
        {
            if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            throw new SharedOutcomeException();
        }
        async Task Execute(BDTestRun run)
        {
            var fixture = new Standalone(run);
            try { await fixture.When(() => ThrowTogether()).Then(() => Complete()).BDTestAsync(); }
            catch (SharedOutcomeException) { }
        }
        await Task.WhenAll(Task.Run(() => Execute(first)), Task.Run(() => Execute(second)));
        Require(first.Scenarios.Single().Status == Status.Skipped, "first run lost frozen skip mapping");
        Require(second.Scenarios.Single().Status == Status.Inconclusive, "second run lost inconclusive mapping");
    }

    private async Task Check(Func<Task<Scenario>> execute, Status expected, int retries, params Status[] steps)
    {
        try { await execute(); }
        finally
        {
            var scenario = TestRun.Scenarios.Single();
            Require(scenario.Status == expected && scenario.RetryCount == retries
                && scenario.Steps.Select(s => s.Status).SequenceEqual(steps),
                $"BDTest mismatch: {scenario.Status}, retries {scenario.RetryCount}, steps {string.Join(", ", scenario.Steps.Select(s => s.Status))}");
            Require(_hooks.Count == 4 * (retries + 1), "scenario hooks did not run once per attempt");
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                var hooks = _hooks.Skip(attempt * 4).Take(4).ToArray();
                Require(hooks.Select(h => h.Name).SequenceEqual(new[] { "run before", "instance before", "instance after", "run after" }),
                    "scenario hook order mismatch");
                Require(hooks.All(h => h.Attempt == attempt), "scenario hook attempt mismatch");
                Require(hooks.Take(2).All(h => h.Status == Status.Pending), "before hooks must see Pending");
                Require(hooks.Skip(2).All(h => h.Status == (attempt < retries ? Status.Failed : expected)),
                    "after hooks must see the attempt's final status");
            }
            Require(_hooks.Where(h => h.Name == "run before").Select(h => h.Context).Distinct().Count() == retries + 1,
                "retry attempts reused contexts");
        }
    }

    private static async Task CheckFailure(Func<Task> execute, string message)
    {
        try { await execute(); }
        catch (Exception e) when (e.Message == message)
        {
            if (Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_RUNNER") == "1") throw;
            return;
        }
        throw new InvalidOperationException("Expected the scenario exception to reach the runner.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

#if BDTEST_NUNIT
    private const Status FrameworkSkipStatus = Status.Skipped;
    private const Status FrameworkInconclusiveStatus = Status.Inconclusive;
    private const bool HasSuccessException = true;
    private static void ReportSuccess() => global::NUnit.Framework.Assert.Pass("conformance success");
    [global::NUnit.Framework.TearDown]
#elif BDTEST_MSTEST
    // MSTest has no runtime skip exception separate from inconclusive.
    private const Status FrameworkSkipStatus = Status.Inconclusive;
    private const Status FrameworkInconclusiveStatus = Status.Inconclusive;
    private const bool HasSuccessException = false;
    private static void ReportSuccess() => Complete();
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanup]
#elif BDTEST_TUNIT
    private const Status FrameworkSkipStatus = Status.Skipped;
    private const Status FrameworkInconclusiveStatus = Status.Inconclusive;
    private const bool HasSuccessException = false;
    private static void ReportSuccess() => Complete();
    [global::TUnit.Core.After(global::TUnit.Core.HookType.Test)]
#elif BDTEST_XUNIT
    // xUnit v3 has runtime skip but no separate inconclusive or success exception.
    private const Status FrameworkSkipStatus = Status.Skipped;
    private const Status FrameworkInconclusiveStatus = Status.Skipped;
    private const bool HasSuccessException = false;
    private static void ReportSuccess() => Complete();
    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _run.Dispose(); }
    }
#endif
#if !BDTEST_XUNIT
    public void DisposeRun()
    {
        EndTest();
        _run.Dispose();
    }
#endif

    private sealed class Standalone(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
    private sealed class TransientException : Exception { }
    private sealed class SharedOutcomeException : Exception { }
    private sealed class SkippedAttribute : SkipStepAttribute { }
    [Skipped] private static void SkippedStep() => MustNotRun();
    private static void Complete() { }
    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
    private static void FailStep() => throw new InvalidOperationException("conformance failure");
    private static void NotImplementedStep() => throw new NotImplementedException("conformance not implemented");
}
