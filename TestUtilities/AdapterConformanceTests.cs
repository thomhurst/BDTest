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
#endif
{
    private readonly List<(string Name, int Attempt, object Context, CancellationToken Token, Status Status)> _hooks = new();
    private readonly BDTestRun _run;
    private AbstractContextBDTestBase<object> _host;
    private readonly bool _composition = false;
    public override BDTestRun TestRun => _run;

#if BDTEST_TUNIT
    public AdapterConformanceTests(bool composition)
    {
        _composition = composition;
        _run = CreateRun();
        _host = composition ? BDTestScenario.For<object>(this, _run) : this;
    }

    BDTestBase global::BDTest.TUnit.IBDTestTUnitTest.BDTestHost => _host;
#else
    public AdapterConformanceTests()
    {
        _run = CreateRun();
        _host = this;
    }
#endif

#if BDTEST_NUNIT
    internal void UseHost(AbstractContextBDTestBase<object> host) => _host = host;
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
        Require(ReferenceEquals(_host.Context, scenario.Context), "scenario hook received another attempt's context");
        _hooks.Add((name, scenario.Attempt, scenario.Context, scenario.CancellationToken, scenario.Scenario.Status));
        if (name.EndsWith("after") && scenario.Scenario.Status is Status.Failed or Status.NotImplemented)
            Require(scenario.Scenario.Exception != null, "after hook did not receive the attempt exception");
        return Task.CompletedTask;
    }

    private sealed class ScenarioHook(AdapterConformanceTests owner) : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => owner.Record("run before", scenario);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => owner.Record("run after", scenario);
    }

    [Test]
    public Task Pass() => Check(() => _host.Given(() => Complete()).When(() => Complete())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Passed, Status.Passed);

    [Test]
    public Task Fail() => CheckFailure(() => Check(() => _host.Given(() => Complete()).When(() => FailStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Failed, 0, Status.Passed, Status.Failed, Status.NotRun),
        "conformance failure");

    [Test]
    public Task NotImplemented() => CheckFailure(() => Check(() => _host.Given(() => Complete()).When(() => NotImplementedStep())
        .Then(() => MustNotRun()).BDTestAsync(), Status.NotImplemented, 0,
        Status.Passed, Status.NotImplemented, Status.NotRun), "conformance not implemented");

    [Test]
    public Task SingleStepSkipped() => Check(() => _host.Given(() => Complete()).When(() => SkippedStep())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0, Status.Passed, Status.Skipped, Status.Passed);

    [Test]
    public Task AllStepsSkipped() => Check(() => _host.Given(() => SkippedStep()).When(() => SkippedStep())
        .Then(() => SkippedStep()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkips() => Check(() => _host.Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test, ScenarioText("Conformance global skip")]
    public Task GlobalRuleSkipsWithDirectAwait() => Check(async () => await _host.Given(() => MustNotRun()).When(() => MustNotRun())
        .Then(() => MustNotRun()), Status.Skipped, 0, Status.Skipped, Status.Skipped, Status.Skipped);

    [Test]
    public Task FrameworkSkip() => Check(() => _host.Given(() => Complete()).When(() => OutcomeMap.ReportSkip("conformance skip"))
        .Then(() => MustNotRun()).BDTestAsync(), FrameworkSkipStatus, 0,
        Status.Passed, FrameworkSkipStatus, FrameworkSkipStatus == Status.Skipped ? Status.Skipped : Status.NotRun);

    [Test]
    public Task FrameworkInconclusive()
    {
        Task Execute() => Check(() => _host.Given(() => Complete())
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
    public Task FrameworkSuccess() => Check(() => _host.Given(() => Complete()).When(() => ReportSuccess())
        .Then(() => Complete()).BDTestAsync(), Status.Passed, 0,
        Status.Passed, Status.Passed, HasSuccessException ? Status.NotRun : Status.Passed);

    [Test]
    public Task RetryThenPass()
    {
        var attempts = 0;
        void FailOnce() { if (++attempts == 1) throw new TransientException(); }
        return Check(() => _host.Given(() => Complete()).When(() => FailOnce()).Then(() => Complete()).BDTestAsync(),
            Status.Passed, 1, Status.Passed, Status.Passed, Status.Passed);
    }

    [Test]
    public async Task ScenarioTimeoutHooks()
    {
        try
        {
            await Check(() => _host.When(async token => await Task.Delay(Timeout.InfiniteTimeSpan, token))
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
        var outline = await _host.ScenarioOutline("example <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
        {
            var calls = 0;
            void FailOnce() { if (++calls == 1) throw new TransientException(); }
            return _host.When(() => FailOnce()).Then(() => Complete());
        }).BDTestAsync();
        Require(outline.Examples.All(s => s.Status == Status.Passed && s.RetryCount == 1), "outline retry mismatch");
        Require(_hooks.Count == (_composition ? 8 : 16), "hooks must run for each row attempt, not the outline container");
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
        var factory = new LifetimeFactory();
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = factory });
        var fixture = new ContextLifetimeProbe(run, factory, _composition);
        await fixture.StartAsync();
        await fixture.InitializeContextAsync();
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
        var factory = new LifetimeFactory(gateDisposal: false);
        var options = new BDTestOptions { ContextFactory = factory };
        options.RetryTestRules.Add(exception => exception is TransientException, 1);
        using var run = BDTestRun.Start(options);
        var fixture = new ContextLifetimeProbe(run, factory, _composition);
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
        var factory = new LifetimeFactory(gateDisposal: false, failDisposal: true);
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = factory });
        var fixture = new ContextLifetimeProbe(run, factory, _composition);
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
            if (_composition)
            {
                var composed = BDTestScenario.For<object>(new object(), run);
                composed.BeginTest("same-id");
                host = composed;
                end = composed.EndTest;
            }
            else
            {
                var inherited = new IsolationProbe(run);
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
            Require(scenario.TestStartupInformation.Contains(label + " setup") && !scenario.TestStartupInformation.Contains(other), "startup output leaked");
            Require(scenario.Output.Contains(label + " step") && !scenario.Output.Contains(other), "step output leaked");
            Require(scenario.TearDownOutput.Contains(label + " teardown") && !scenario.TearDownOutput.Contains(other), "teardown output leaked");
            var report = BDTest.Helpers.BDTestJsonHelper.Deserialize(BDTest.Helpers.BDTestJsonHelper.GetTestJsonData(null, run));
            Require(report.Scenarios.Single().ReportId == run.ReportId, "report ownership leaked");
        }
    }

    private sealed class IsolationProbe(BDTestRun run) : Adapter
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "same-id";
        public void Start() => BeginTest("same-id");
        public void Finish() => EndTest();
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
            var hookNames = _composition
                ? new[] { "run before", "run after" }
                : new[] { "run before", "instance before", "instance after", "run after" };
            Require(_hooks.Count == hookNames.Length * (retries + 1), "scenario hooks did not run once per attempt");
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                var hooks = _hooks.Skip(attempt * hookNames.Length).Take(hookNames.Length).ToArray();
                Require(hooks.Select(h => h.Name).SequenceEqual(hookNames),
                    "scenario hook order mismatch");
                Require(hooks.All(h => h.Attempt == attempt), "scenario hook attempt mismatch");
                Require(hooks.Take(hookNames.Length / 2).All(h => h.Status == Status.Pending), "before hooks must see Pending");
                Require(hooks.Skip(hookNames.Length / 2).All(h => h.Status == (attempt < retries ? Status.Failed : expected)),
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
#else
        try { await EndTestAsync(); }
#endif
        finally { _run.Dispose(); }
    }
#endif

#if BDTEST_NUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run, LifetimeFactory factory, bool composition) : BDTest.NUnit.NUnitBDTestBase<LifetimeContext>
#elif BDTEST_MSTEST
    private sealed class ContextLifetimeProbe(BDTestRun run, LifetimeFactory factory, bool composition) : BDTest.MSTest.MSTestBDTestBase<LifetimeContext>
#elif BDTEST_TUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run, LifetimeFactory factory, bool composition) : BDTest.TUnit.TUnitBDTestBase<LifetimeContext>
#elif BDTEST_XUNIT
    private sealed class ContextLifetimeProbe(BDTestRun run, LifetimeFactory factory, bool composition) : BDTest.xUnit.XUnitBDTestBase<LifetimeContext>
#endif
    {
        public override BDTestRun TestRun => run;
        private BDTestScenario<LifetimeContext>? _composed;
        public AbstractContextBDTestBase<LifetimeContext> Host => composition
            ? _composed ??= BDTestScenario.For<LifetimeContext>(this, run) : this;
        public List<LifetimeContext> Contexts => factory.Contexts;
        public new LifetimeContext Context => Host.Context;
        public new ValueTask InitializeContextAsync() => Host.InitializeContextAsync();

        public async Task StartAsync()
        {
            if (composition)
            {
                Host.BeginTest(Guid.NewGuid().ToString("N"), null);
                return;
            }
#if BDTEST_XUNIT
            await ((Xunit.IAsyncLifetime)this).InitializeAsync();
#else
            BeginTest(Guid.NewGuid().ToString("N"));
            await Task.CompletedTask;
#endif
        }

        public Task FinishAsync()
        {
            if (composition) return Host.EndTestAsync().AsTask();
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

    private sealed class LifetimeFactory(bool gateDisposal = true, bool failDisposal = false) : IBDTestContextFactory
    {
        public List<LifetimeContext> Contexts { get; } = new();

        public async ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation)
        {
            await Task.Yield();
            var context = new LifetimeContext(creation.Attempt, gateDisposal, failDisposal);
            Contexts.Add(context);
            return context;
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
