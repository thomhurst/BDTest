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
#elif BDTEST_TUNIT
[global::TUnit.Core.Arguments(false)]
[global::TUnit.Core.Arguments(true)]
#endif
public class AdapterConformanceTests : Adapter
#if BDTEST_TUNIT
    , global::BDTest.TUnit.IBDTestTUnitTest
#elif BDTEST_MSTEST
    , global::BDTest.MSTest.IBDTestMSTestTest
#endif
{
    private readonly List<(string Name, int Attempt, object Context, CancellationToken Token, Status Status)> _hooks = new();
    private readonly BDTestRun _run;
#if BDTEST_NUNIT
    private AbstractContextBDTestBase<object>? _nunitHost;
#endif

#if BDTEST_TUNIT
    private readonly BDTestScenario<object>? _tunitHost;
    protected virtual AbstractContextBDTestBase<object> ScenarioHost => _tunitHost ?? (AbstractContextBDTestBase<object>)this;
    BDTestBase global::BDTest.TUnit.IBDTestTUnitTest.BDTestHost => ScenarioHost;

    public AdapterConformanceTests(bool composition)
    {
        _run = CreateRun();
        if (composition) _tunitHost = BDTestScenario.For<object>(this, _run);
    }
#else
#if BDTEST_NUNIT
    protected virtual AbstractContextBDTestBase<object> ScenarioHost => _nunitHost ?? this;
#elif BDTEST_MSTEST
    private readonly BDTestScenario<object>? _mstestHost;
    protected virtual AbstractContextBDTestBase<object> ScenarioHost => _mstestHost ?? (AbstractContextBDTestBase<object>)this;
    BDTestBase global::BDTest.MSTest.IBDTestMSTestTest.BDTestHost => ScenarioHost;
#else
    protected virtual AbstractContextBDTestBase<object> ScenarioHost => this;
#endif
    public AdapterConformanceTests()
    {
        _run = CreateRun();
#if BDTEST_MSTEST
        if (this is MSTestCompositionAdapterConformanceTests)
            _mstestHost = BDTestScenario.For<object>(this, _run);
#endif
    }
#endif
    public override BDTestRun TestRun => _run;

#if BDTEST_NUNIT
    internal void UseHost(AbstractContextBDTestBase<object> host) => _nunitHost = host;
#endif

#if BDTEST_NUNIT
    private bool IncludesInstanceHooks => true;
#else
    private bool IncludesInstanceHooks => ReferenceEquals(ScenarioHost, this);
#endif

    private BDTestRun CreateRun()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(OutcomeMap);
        options.RegisterFrameworkOutcomes(OutcomeMap);
        options.SkipStepRules.Add<SkippedAttribute>(_ => true);
        options.SkipTestRules.Add(s => s.GetScenarioText() == "Conformance global skip");
        options.RetryTestRules.Add(e => e is TransientException, 1);
        options.ScenarioHooks.Add(new ScenarioHook(this));
        return BDTestRun.Start(options);
    }

    public override Task OnBeforeScenario(ScenarioHookContext<object> scenario) => Record("instance before", scenario);
    public override Task OnAfterScenario(ScenarioHookContext<object> scenario) => Record("instance after", scenario);

    internal Task Record(string name, ScenarioHookContext scenario)
    {
        Require(ReferenceEquals(ScenarioHost.Context, scenario.Context), "scenario hook received another attempt's context");
        _hooks.Add((name, scenario.Attempt, scenario.Context ?? throw new InvalidOperationException("Expected the scenario hook context."), scenario.CancellationToken, scenario.Scenario.Status));
        if (name.EndsWith("after") && scenario.Scenario.Status is Status.Failed or Status.NotImplemented)
            Require(scenario.Scenario.Exception != null, "after hook did not receive the attempt exception");
        return Task.CompletedTask;
    }

    private sealed class ScenarioHook(AdapterConformanceTests owner) : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) =>
            owner.Record("run before", scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) =>
            owner.Record("run after", scenario);
    }

    [Test]
    public Task Pass() => Check(() => ScenarioHost.Given(() => Complete()).When(() => Complete())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Passed, Status.Passed);

    [Test]
    public Task Fail() => CheckFailure(() => Check(() => ScenarioHost.Given(() => Complete()).When(() => FailStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Failed, 0, Status.Passed, Status.Failed, Status.NotRun),
        "conformance failure");

    [Test]
    public Task NotImplemented() => CheckFailure(() => Check(() => ScenarioHost.Given(() => Complete()).When(() => NotImplementedStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.NotImplemented, 0,
        Status.Passed, Status.NotImplemented, Status.NotRun), "conformance not implemented");

    [Test]
    public Task SingleStepSkipped() => Check(() => ScenarioHost.Given(() => Complete()).When(() => SkippedStep())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Skipped, Status.Passed);

    [Test]
    public Task AllStepsSkipped() => Check(() => ScenarioHost.Given(() => SkippedStep()).When(() => SkippedStep())
        .Then(() => SkippedStep()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkips() => Check(() => ScenarioHost.Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkipsWithDirectAwait() => Check(async () => await ScenarioHost.Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test]
    public Task FrameworkSkip() => Check(() => ScenarioHost.Given(() => Complete()).When(() => OutcomeMap.ReportSkip("conformance skip"))
        .Then(() => MustNotRun()).BDTestAsync(), FrameworkSkipStatus, 0,
        Status.Passed, FrameworkSkipStatus, FrameworkSkipStatus == Status.Skipped ? Status.Skipped : Status.NotRun);

    [Test]
    public Task FrameworkInconclusive()
    {
        Task Execute() => Check(() => ScenarioHost.Given(() => Complete())
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
    public Task FrameworkSuccess() => Check(() => ScenarioHost.Given(() => Complete()).When(() => ReportSuccess())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0,
        Status.Passed, Status.Passed, HasSuccessException ? Status.NotRun : Status.Passed);

    [Test]
    public Task RetryThenPass()
    {
        var attempts = 0;
        void FailOnce() { if (++attempts == 1) throw new TransientException(); }
        return Check(() => ScenarioHost.Given(() => Complete()).When(() => FailOnce()).Then(() => Complete()).BDTestAsync(),
            Status.Passed, 1, Status.Passed, Status.Passed, Status.Passed);
    }

    [Test]
    public async Task ScenarioTimeoutHooks()
    {
        try
        {
            await Check(() => ScenarioHost.When(async token => await Task.Delay(Timeout.InfiniteTimeSpan, token))
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
        var outline = await ScenarioHost.ScenarioOutline("example <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
        {
            var calls = 0;
            void FailOnce() { if (++calls == 1) throw new TransientException(); }
            return ScenarioHost.When(() => FailOnce()).Then(() => Complete());
        }).BDTestAsync();
        var examples = outline.Examples ?? throw new InvalidOperationException("Expected outline examples.");
        Require(examples.All(s => s.Status == Status.Passed && s.RetryCount == 1), "outline retry mismatch");
        Require(_hooks.Count == (IncludesInstanceHooks ? 16 : 8), "hooks must run for each row attempt, not the outline container");
        Require(_hooks.Where(h => h.Name == "run before").Select(h => h.Context).Distinct().Count() == 4,
            "outline attempts reused contexts");
        Require(_hooks.Where(h => h.Name == "run before").Select(h => h.Token).Distinct().Count() == 4,
            "outline attempts reused cancellation tokens");
        Require(_hooks.Where(h => h.Name == "run after").Select(h => h.Status)
            .SequenceEqual(new[] { Status.Failed, Status.Passed, Status.Failed, Status.Passed }), "outline hook outcomes mismatch");
    }

    [Test]
    public async Task ContextFactoryInitializationAndAsyncDisposal()
    {
        using var fixture = CreateLifetimeProbe();
        await fixture.StartAsync();
        await fixture.Host.InitializeContextAsync();
        var context = fixture.Context;
        Require(context.Attempt == 0, "factory did not receive the zero-based initial attempt");
        Require(context.InitializationCalls == 1, "explicit context initialization was not awaited");
        await fixture.Host.When(() => Require(context.InitializationCalls == 1, "step ran before initialization"))
            .Then(() => Complete()).BDTestAsync();
        Require(context.InitializationCalls == 1, "scenario initialized the context more than once");

        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#if BDTEST_XUNIT
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
#else
        var cancellationToken = CancellationToken.None;
#endif
        // Release even if a regression blocks inside the teardown entry point.
        var release = Task.Run(async () =>
        {
            await context.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            try { await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            finally { context.ReleaseDisposal.TrySetResult(); }
        }, cancellationToken);
        var completion = fixture.FinishAsync();
        returned.SetResult();
        await release;
        await completion;
        Require(context.DisposalCalls == 1 && context.Disposed, "adapter did not await context disposal");
        await fixture.FinishAsync();
        Require(context.DisposalCalls == 1, "adapter disposed the context more than once");
    }

    [Test]
    public async Task ContextRetryDisposesPreviousAttempt()
    {
        using var fixture = CreateLifetimeProbe(gateDisposal: false, retry: true);
        await fixture.StartAsync();
        void RetryOnce()
        {
            var context = fixture.Context;
            Require(context.InitializationCalls == 1, "retry step ran before context initialization");
            if (context.Attempt == 0) throw new TransientException();
            Require(context.Attempt == 1, "factory received an incorrect retry attempt");
            Require(fixture.Contexts[0].Disposed, "retry initialized before previous context disposal completed");
        }

        try
        {
            var scenario = await fixture.Host.When(() => RetryOnce()).Then(() => Complete()).BDTestAsync();
            Require(scenario.Status == Status.Passed && scenario.RetryCount == 1, "retry did not pass on its second attempt");
            Require(fixture.Contexts.Count == 2, "retry did not create exactly one context per attempt");
            Require(fixture.Contexts[0].DisposalCalls == 1, "previous attempt was not disposed exactly once");
            Require(!fixture.Contexts[1].Disposed, "final context was disposed before adapter teardown");
        }
        finally
        {
            await fixture.FinishAsync();
        }

        Require(fixture.Contexts.All(context => context.InitializationCalls == 1 &&
            context.DisposalCalls == 1 && context.Disposed), "attempt contexts were not initialized and disposed exactly once");
    }

    [Test]
    public async Task ContextDisposalFailureReachesRunner()
    {
        using var fixture = CreateLifetimeProbe(gateDisposal: false, failDisposal: true);
        await fixture.StartAsync();
        await fixture.Host.When(() => Complete()).Then(() => Complete()).BDTestAsync();
        var context = fixture.Context;
        try
        {
            await CheckFailure(fixture.FinishAsync, "conformance context disposal");
        }
        finally
        {
            Require(context.Disposed && context.DisposalCalls == 1, "throwing context disposal was not awaited");
            await fixture.FinishAsync();
            Require(context.DisposalCalls == 1, "failed disposal was repeated by adapter teardown");
        }
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
#if BDTEST_XUNIT
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
#else
        var cancellationToken = CancellationToken.None;
#endif
        await Task.WhenAll(Task.Run(() => Execute(first), cancellationToken), Task.Run(() => Execute(second), cancellationToken));
        Require(first.Scenarios.Single().Status == Status.Skipped, "first run lost frozen skip mapping");
        Require(second.Scenarios.Single().Status == Status.Inconclusive, "second run lost inconclusive mapping");
    }

    [Test]
    public async Task ExplicitRunsIsolateContextOutputAndCompletion()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Scenario> Execute(BDTestRun run, string label)
        {
            AbstractContextBDTestBase<object> host;
            Action end;
            if (!ReferenceEquals(ScenarioHost, this))
            {
                var composed = BDTestScenario.For<object>(new object(), run);
                composed.BeginTest("same-id");
                host = composed;
                end = composed.EndTest;
            }
            else
            {
                var inherited = new CoreIsolationProbe(run);
                inherited.Start();
                host = inherited;
                end = inherited.Finish;
            }
            var context = host.Context;
            try
            {
                Console.WriteLine(label + " setup");
                var scenario = await host.When(async () =>
                {
                    Console.WriteLine(label + " step");
                    if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Require(ReferenceEquals(context, host.Context), "parallel runs changed context");
                }).Then(() => Complete()).BDTestAsync();
                Console.WriteLine(label + " teardown");
                return scenario;
            }
            finally
            {
                end();
                end();
                Require(!ReferenceEquals(context, host.Context), "completion retained context");
            }
        }
        await Task.WhenAll(Execute(first, "first"), Execute(second, "second"));
        foreach (var (run, label, other) in new[] { (first, "first", "second"), (second, "second", "first") })
        {
            var scenario = run.Scenarios.Single();
            Require(scenario.FrameworkTestId == "same-id" && scenario.ReportId == run.ReportId, "run identity leaked");
            var startupOutput = scenario.TestStartupInformation
                ?? throw new InvalidOperationException("Expected startup output.");
            var stepOutput = scenario.Output ?? throw new InvalidOperationException("Expected step output.");
            var teardownOutput = scenario.TearDownOutput
                ?? throw new InvalidOperationException("Expected teardown output.");
            Require(startupOutput.Contains(label + " setup") && !startupOutput.Contains(other), "startup output leaked");
            Require(stepOutput.Contains(label + " step") && !stepOutput.Contains(other), "step output leaked");
            Require(teardownOutput.Contains(label + " teardown") && !teardownOutput.Contains(other), "teardown output leaked");
            var report = BDTest.Helpers.BDTestJsonHelper.Deserialize(BDTest.Helpers.BDTestJsonHelper.GetTestJsonData(null, run))
                ?? throw new InvalidOperationException("Expected the run report.");
            var scenarios = report.Scenarios ?? throw new InvalidOperationException("Expected report scenarios.");
            Require(scenarios.Single().ReportId == run.ReportId, "report ownership leaked");
        }
    }

    private sealed class CoreIsolationProbe(BDTestRun run) : Adapter
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "same-id";
        public void Start() => BeginTest("same-id");
        public void Finish() => EndTest();
    }

#if BDTEST_XUNIT
    [Test]
    public async Task ExplicitAdapterRunsIsolateContextOutputAndCompletion()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        (AbstractContextBDTestBase<IsolationContext> Host, Func<ValueTask> End) Start(BDTestRun run)
        {
            if (!ReferenceEquals(ScenarioHost, this))
            {
                var host = global::BDTest.xUnit.XUnitBDTestScenario.For<IsolationContext>(this, run);
                return (host, host.EndTestAsync);
            }
            var fixture = new IsolationProbe(run);
            ((Xunit.IAsyncLifetime)fixture).InitializeAsync().GetAwaiter().GetResult();
            return (fixture, fixture.DisposeAsync);
        }
        var one = Start(first);
        var firstContext = one.Host.Context;
        one.Host.WriteStartupOutput("first run only");
        var two = Start(second);
        var secondContext = two.Host.Context;
        two.Host.WriteStartupOutput("second run only");
        try
        {
            async Task Execute(AbstractContextBDTestBase<IsolationContext> host, string output) =>
                await host.When(async () => { await Task.Yield(); Console.WriteLine(output); })
                    .Then(() => Complete()).BDTestAsync();
            var runnerToken = Xunit.TestContext.Current.CancellationToken;
            await Task.WhenAll(Task.Run(() => Execute(one.Host, "first step"), runnerToken),
                Task.Run(() => Execute(two.Host, "second step"), runnerToken));
            var firstScenario = first.Scenarios.Single();
            var secondScenario = second.Scenarios.Single();
            Require(firstScenario.FrameworkTestId == secondScenario.FrameworkTestId, "runner identity changed between explicit runs");
            Require(!ReferenceEquals(firstContext, secondContext), "runs reused a context");
            Require(firstScenario.TestStartupInformation.Contains("first run only") &&
                !firstScenario.TestStartupInformation.Contains("second run only"), "first run startup leaked");
            Require(secondScenario.TestStartupInformation.Contains("second run only") &&
                !secondScenario.TestStartupInformation.Contains("first run only"), "second run startup leaked");
            Require(firstScenario.Steps[0].Output.Contains("first step") &&
                !firstScenario.Steps[0].Output.Contains("second step"), "first run step output leaked");
            Require(secondScenario.Steps[0].Output.Contains("second step") &&
                !secondScenario.Steps[0].Output.Contains("first step"), "second run step output leaked");
        }
        finally
        {
            await one.End();
            await two.End();
        }
        Require(firstContext.Disposed && secondContext.Disposed, "adapter did not complete both runs");
        await one.End();
        await two.End();
    }

    private sealed class IsolationProbe(BDTestRun run) : global::BDTest.xUnit.XUnitBDTestBase<IsolationContext>
    {
        public override BDTestRun TestRun => run;
    }

    public sealed class IsolationContext : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Require(!Disposed, "adapter disposed context twice");
            Disposed = true;
        }
    }
#endif

    private async Task Check(Func<Task<Scenario>> execute, Status expected, int retries, params Status[] steps)
    {
        try { await execute(); }
        finally
        {
            var scenario = TestRun.Scenarios.Single();
            var scenarioSteps = scenario.Steps ?? throw new InvalidOperationException("Expected scenario steps.");
            Require(scenario.Status == expected && scenario.RetryCount == retries
                && scenarioSteps.Select(s => s.Status).SequenceEqual(steps),
                $"BDTest mismatch: {scenario.Status}, retries {scenario.RetryCount}, steps {string.Join(", ", scenarioSteps.Select(s => s.Status))}");
            var names = IncludesInstanceHooks
                ? new[] { "run before", "instance before", "instance after", "run after" }
                : new[] { "run before", "run after" };
            Require(_hooks.Count == names.Length * (retries + 1), "scenario hooks did not run once per attempt");
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                var hooks = _hooks.Skip(attempt * names.Length).Take(names.Length).ToArray();
                Require(hooks.Select(h => h.Name).SequenceEqual(names), "scenario hook order mismatch");
                Require(hooks.All(h => h.Attempt == attempt), "scenario hook attempt mismatch");
                Require(hooks.Where(h => h.Name.EndsWith("before")).All(h => h.Status == Status.Pending), "before hooks must see Pending");
                Require(hooks.Where(h => h.Name.EndsWith("after")).All(h => h.Status == (attempt < retries ? Status.Failed : expected)),
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
    public async Task DisposeRun()
    {
#if BDTEST_TUNIT
        try { await global::BDTest.TUnit.TUnitBDTestLifecycle.AfterTest(global::TUnit.Core.TestContext.Current!); }
#elif BDTEST_MSTEST
        try { await ScenarioHost.EndTestAsync(); }
#else
        try { await EndTestAsync(); }
#endif
        finally { _run.Dispose(); }
    }
#endif

    private LifetimeProbe CreateLifetimeProbe(bool gateDisposal = true, bool failDisposal = false, bool retry = false)
    {
        var factory = new LifetimeFactory(gateDisposal, failDisposal);
        var options = new BDTestOptions { ContextFactory = factory };
        if (retry) options.RetryTestRules.Add(exception => exception is TransientException, 1);
        var run = BDTestRun.Start(options);
#if BDTEST_XUNIT
        if (!ReferenceEquals(ScenarioHost, this))
        {
            var host = global::BDTest.xUnit.XUnitBDTestScenario.For<LifetimeContext>(this, run);
            return new LifetimeProbe(run, host, factory.Contexts,
                () => Task.CompletedTask, () => host.EndTestAsync().AsTask());
        }
#endif
#if BDTEST_TUNIT
        if (!ReferenceEquals(ScenarioHost, this))
        {
            var host = BDTestScenario.For<LifetimeContext>(this, run);
            return new LifetimeProbe(run, host, factory.Contexts,
                () => { host.BeginTest(Guid.NewGuid().ToString("N")); return Task.CompletedTask; },
                () => host.EndTestAsync().AsTask());
        }
#endif
        var fixture = new ContextLifetimeProbe(run);
        return new LifetimeProbe(run, fixture, factory.Contexts, fixture.StartAsync, fixture.FinishAsync);
    }

    private sealed class LifetimeFactory(bool gateDisposal, bool failDisposal) : IBDTestContextFactory
    {
        public List<LifetimeContext> Contexts { get; } = new();
        public async ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation)
        {
            await Task.Yield();
            Require(contextType == typeof(LifetimeContext), "factory received an incorrect context type");
            var context = new LifetimeContext(creation.Attempt, gateDisposal, failDisposal);
            Contexts.Add(context);
            return context;
        }
    }

    private sealed class LifetimeProbe(BDTestRun run, AbstractContextBDTestBase<LifetimeContext> host,
        List<LifetimeContext> contexts, Func<Task> start, Func<Task> finish) : IDisposable
    {
        public AbstractContextBDTestBase<LifetimeContext> Host => host;
        public LifetimeContext Context => host.Context;
        public List<LifetimeContext> Contexts => contexts;
        public Task StartAsync() => start();
        public Task FinishAsync() => finish();
        public void Dispose() => run.Dispose();
    }

#if BDTEST_NUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run) : BDTest.NUnit.NUnitBDTestBase<LifetimeContext>
#elif BDTEST_MSTEST
    private sealed class ContextLifetimeProbe(BDTestRun run) : BDTest.MSTest.MSTestBDTestBase<LifetimeContext>
#elif BDTEST_TUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run) : BDTest.TUnit.TUnitBDTestBase<LifetimeContext>
#elif BDTEST_XUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run) : BDTest.xUnit.XUnitBDTestBase<LifetimeContext>
#endif
    {
        public override BDTestRun TestRun => run;
        public async Task StartAsync()
        {
#if BDTEST_XUNIT
            await ((Xunit.IAsyncLifetime)this).InitializeAsync();
#else
            BeginTest(Guid.NewGuid().ToString("N"));
            await Task.CompletedTask;
#endif
        }

        public Task FinishAsync()
        {
#if BDTEST_NUNIT
            return BDTestNUnitTearDown();
#elif BDTEST_MSTEST
            return BDTestMSTestTearDown();
#elif BDTEST_TUNIT
            return BDTestTUnitTearDown();
#elif BDTEST_XUNIT
            return DisposeAsync().AsTask();
#endif
        }
    }

    private sealed class LifetimeContext(int attempt, bool gateDisposal, bool failDisposal) : IBDTestContextLifetime, IAsyncDisposable
    {
        public LifetimeContext() : this(0, false, false) { }
        public int Attempt { get; } = attempt;
        public int InitializationCalls { get; private set; }
        public int DisposalCalls { get; private set; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask InitializeAsync()
        {
            await Task.Yield();
            InitializationCalls++;
        }

        public async ValueTask DisposeAsync()
        {
            DisposalCalls++;
            DisposalStarted.SetResult();
            if (gateDisposal) await ReleaseDisposal.Task;
            await Task.Yield();
            Disposed = true;
            if (failDisposal) throw new InvalidOperationException("conformance context disposal");
        }
    }

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

#if BDTEST_NUNIT
// Exercise the same contract through the runner extension on an unrelated fixture base.
public abstract class CompositionConformanceFixtureBase { }

[global::BDTest.NUnit.BDTestFixture]
[global::NUnit.Framework.FixtureLifeCycle(global::NUnit.Framework.LifeCycle.InstancePerTestCase)]
public class CompositionAdapterConformanceTests : CompositionConformanceFixtureBase, BDTest.NUnit.INUnitBDTest
{
    internal AdapterConformanceTests Cases { get; } = new();
    private readonly CompositionHost _host;
    BDTestBase BDTest.NUnit.INUnitBDTest.BDTestHost => _host;

    public CompositionAdapterConformanceTests()
    {
        _host = new CompositionHost(this, Cases.TestRun);
        Cases.UseHost(_host);
    }

    private sealed class CompositionHost(CompositionAdapterConformanceTests owner, BDTestRun run)
        : BDTest.NUnit.NUnitBDTestScenario<object>(owner, run)
    {
        public override Task OnBeforeScenario(ScenarioHookContext<object> scenario) =>
            owner.Cases.Record("instance before", scenario);
        public override Task OnAfterScenario(ScenarioHookContext<object> scenario) =>
            owner.Cases.Record("instance after", scenario);
    }

    [Test] public Task Pass() => Cases.Pass();
    [Test] public Task Fail() => Cases.Fail();
    [Test] public Task NotImplemented() => Cases.NotImplemented();
    [Test] public Task SingleStepSkipped() => Cases.SingleStepSkipped();
    [Test] public Task AllStepsSkipped() => Cases.AllStepsSkipped();
    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkips() => Cases.GlobalRuleSkips();
    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkipsWithDirectAwait() => Cases.GlobalRuleSkipsWithDirectAwait();
    [Test] public Task FrameworkSkip() => Cases.FrameworkSkip();
    [Test] public Task FrameworkInconclusive() => Cases.FrameworkInconclusive();
    [Test] public Task FrameworkSuccess() => Cases.FrameworkSuccess();
    [Test] public Task RetryThenPass() => Cases.RetryThenPass();
    [Test] public Task ScenarioTimeoutHooks() => Cases.ScenarioTimeoutHooks();
    [Test] public Task ScenarioOutlineHooks() => Cases.ScenarioOutlineHooks();
    [Test] public Task ContextFactoryInitializationAndAsyncDisposal() => Cases.ContextFactoryInitializationAndAsyncDisposal();
    [Test] public Task ContextRetryDisposesPreviousAttempt() => Cases.ContextRetryDisposesPreviousAttempt();
    [Test] public Task ContextDisposalFailureReachesRunner() => Cases.ContextDisposalFailureReachesRunner();
    [Test] public Task ExplicitRunsIsolateContextOutputAndCompletion() => Cases.ExplicitRunsIsolateContextOutputAndCompletion();
    [Test] public Task ExplicitRunsUseDifferentMappings() => Cases.ExplicitRunsUseDifferentMappings();

    private static readonly System.Collections.Concurrent.ConcurrentBag<BDTestRun> Runs = new();
    [global::NUnit.Framework.SetUp]
    public void TrackRun() => Runs.Add(Cases.TestRun);

    [global::NUnit.Framework.OneTimeTearDown]
    public static void DisposeRuns()
    {
        while (Runs.TryTake(out var run))
        {
            try
            {
                if (run.OutputBuffers.Count != 0)
                    throw new InvalidOperationException("NUnit composition wrapper did not complete output capture");
            }
            finally { run.Dispose(); }
        }
    }
}
#endif

#if BDTEST_MSTEST
// Inherited test methods run the identical contract against a composed host.
[global::BDTest.MSTest.BDTestClass]
public class MSTestCompositionAdapterConformanceTests : AdapterConformanceTests;
#endif

#if BDTEST_XUNIT
// Inherit the shared runner probes, but execute every scenario through composition.
// CompositionTests separately proves a fixture with an unrelated base class.
public class CompositionAdapterConformanceTests : AdapterConformanceTests
{
    private readonly BDTestScenario<object> _host;
    protected override AbstractContextBDTestBase<object> ScenarioHost => _host;

    public CompositionAdapterConformanceTests() =>
        _host = global::BDTest.xUnit.XUnitBDTestScenario.For<object>(this, TestRun);

    public override async ValueTask DisposeAsync()
    {
        try { await ScenarioHost.EndTestAsync(); }
        finally { await base.DisposeAsync(); }
    }
}

#endif
