using System.Collections.Concurrent;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace BDTest.Tests.Fixtures;

public class NUnitCompositionTests
{
    [TestCase(typeof(NUnitComposedRunProbe))]
    [TestCase(typeof(NUnitInheritedRunProbe))]
    public void Native_lifecycle_isolates_two_runs_rows_repeats_and_async_cleanup(Type fixture)
    {
        using var first = NUnitCompositionEvidence.Start("first");
        using var second = NUnitCompositionEvidence.Start("second");
        NUnitCompositionEvidence.Contexts.Clear();
        var results = Run(fixture);
        Assert.That(results, Has.Length.EqualTo(4));
        Assert.That(results.Select(result => result.ResultState.Status), Is.All.EqualTo(TestStatus.Passed),
            string.Join(Environment.NewLine, results.Select(result => result.Message)));
        foreach (var (run, label, other) in new[] { (first, "first", "second"), (second, "second", "first") })
        {
            Assert.That(run.Scenarios, Has.Count.EqualTo(4));
            Assert.That(run.Scenarios.Select(s => s.FrameworkTestId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(run.Scenarios.Select(s => s.ReportId), Is.All.EqualTo(run.ReportId));
            Assert.That(run.OutputBuffers, Is.Empty);
            foreach (var scenario in run.Scenarios)
            {
                Assert.That(scenario.TestStartupInformation, Does.Contain(label + " setup").And.Not.Contain(other));
                Assert.That(scenario.Output, Does.Contain(label + " step").And.Not.Contain(other));
                Assert.That(scenario.TearDownOutput, Does.Contain(label + " teardown").And.Not.Contain(other));
                Assert.That(scenario.GetScenarioText(), Is.EqualTo("Native row metadata"));
                Assert.That(scenario.Tags, Is.EquivalentTo(new[] { "native-row", "native-class" }));
                Assert.That(scenario.RetryCount, Is.EqualTo(1));
            }
            var report = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run));
            Assert.That(report.Scenarios.Select(s => s.ReportId), Is.All.EqualTo(run.ReportId));
        }
        var contexts = NUnitCompositionEvidence.Contexts.ToArray();
        Assert.That(contexts, Has.Length.EqualTo(16));
        Assert.That(contexts.Distinct().Count(), Is.EqualTo(16));
        Assert.That(contexts.All(context => context.Disposals == 1 && context.Disposed), Is.True);
    }

    [TestCase(typeof(NUnitComposedFailureProbe))]
    [TestCase(typeof(NUnitInheritedFailureProbe))]
    public void Completion_survives_setup_body_teardown_and_disposal_failures(Type fixture)
    {
        using var run = NUnitCompositionEvidence.Start("failure");
        NUnitCompositionEvidence.Contexts.Clear();
        var results = Run(fixture);
        Assert.That(results, Has.Length.EqualTo(5));
        Assert.That(results.Select(result => result.ResultState.Status), Is.All.EqualTo(TestStatus.Failed));
        foreach (var mode in new[] { "setup", "body", "teardown", "dispose", "body-dispose" })
        {
            var result = results.Single(result => result.Name == $"Run(\"{mode}\")");
            foreach (var diagnostic in mode.Split('-'))
                Assert.That(result.Message, Does.Contain("native " + diagnostic));
        }
        Assert.That(run.OutputBuffers, Is.Empty);
        Assert.That(NUnitCompositionEvidence.Contexts, Has.Count.EqualTo(5));
        Assert.That(NUnitCompositionEvidence.Contexts.All(context => context.Disposals == 1 && context.Disposed), Is.True);
    }

    [Test]
    public async Task Completion_starts_disposal_outside_runner_synchronization_context_and_clears_capture()
    {
        using var run = BDTestRun.Start();
        var owner = new SynchronizationProbe(run);
        var synchronization = new QueuedSynchronizationContext();
        var execution = Task.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronization);
                var test = new TestMethod(new MethodWrapper(typeof(SynchronizationProbe), nameof(SynchronizationProbe.Body)));
                var context = new TestExecutionContext
                {
                    TestObject = owner, CurrentTest = test, CurrentResult = test.MakeTestResult()
                };
                var command = new BDTestFixtureAttribute().Wrap(new ContextCommand(test, owner));
                var result = command.Execute(context);
                return (result.ResultState.Status, BDTest.Output.TestOutputData.FrameworkExecutionId);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
        // If cleanup captures the blocked runner context, release its continuation
        // before failing so the test cannot strand a worker thread.
        var completed = await Task.WhenAny(execution, Task.Delay(TimeSpan.FromSeconds(5))) == execution;
        synchronization.Drain();
        var (status, capture) = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(completed, Is.True, "Context disposal captured the blocked runner synchronization context.");
        Assert.That(status, Is.EqualTo(TestStatus.Passed));
        Assert.That(capture, Is.Null);
        Assert.That(owner.Resource.Disposed, Is.True);
    }

    private sealed class SynchronizationProbe : INUnitBDTest
    {
        internal readonly NUnitBDTestScenario<NUnitNativeContext> Host;
        internal NUnitNativeContext Resource;
        public SynchronizationProbe(BDTestRun run) => Host = NUnitBDTestScenario.For<NUnitNativeContext>(this, run);
        public BDTestBase BDTestHost => Host;
        public void Body() => Resource = Host.Context;
    }

    private sealed class ContextCommand(TestMethod test, SynchronizationProbe owner) : TestCommand(test)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            owner.Body();
            context.CurrentResult.SetResult(ResultState.Success);
            return context.CurrentResult;
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _continuations = new();
        public override void Post(SendOrPostCallback callback, object state) => _continuations.Enqueue(() => callback(state));
        internal void Drain() { while (_continuations.TryDequeue(out var continuation)) continuation(); }
    }

    private static ITestResult[] Run(Type fixture)
    {
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(fixture.Assembly, new Dictionary<string, object> { ["NumberOfTestWorkers"] = 4 });
        return Leaves(runner.Run(TestListener.NULL,
            TestFilter.FromXml($"<filter><class>{fixture.FullName}</class></filter>"))).ToArray();
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) => result.HasChildren
        ? result.Children.SelectMany(Leaves) : new[] { result };
}

internal static class NUnitCompositionEvidence
{
    private static readonly ConcurrentDictionary<string, BDTestRun> Runs = new();
    internal static readonly ConcurrentBag<NUnitNativeContext> Contexts = new();

    internal static BDTestRun Start(string label)
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(NUnitBDTestScenario.OutcomeMap);
        options.ContextFactory = new NativeContextFactory(label);
        return Runs[label] = BDTestRun.Start(options);
    }
    private sealed class NativeContextFactory(string label) : IBDTestContextFactory
    {
        public async ValueTask<object> CreateContextAsync(Type contextType, ContextCreation creation)
        {
            await Task.Yield();
            Assert.That(contextType, Is.EqualTo(typeof(NUnitNativeContext)));
            return new NUnitNativeContext { Label = label, Attempt = creation.Attempt };
        }
    }

    internal static BDTestRun Get(string label) => Runs[label];

    internal static async Task Setup(AbstractContextBDTestBase<NUnitNativeContext> host, string label)
    {
        await host.InitializeContextAsync();
        Assert.That(host.Context.Label, Is.EqualTo(label));
        host.Context.FailDisposal = TestContext.CurrentContext.Test.Arguments.FirstOrDefault() is string mode && mode.Contains("dispose");
        Console.WriteLine(label + " setup");
        if (TestContext.CurrentContext.Test.Arguments.FirstOrDefault() is "setup")
            throw new InvalidOperationException("native setup");
    }

    internal static async Task Execute(AbstractContextBDTestBase<NUnitNativeContext> host, string label)
    {
        var original = host.Context;
        var attempts = 0;
        await host.When(async () =>
        {
            await Task.Yield();
            Console.WriteLine(label + " step");
            Assert.That(host.Context.Initialized, Is.True);
            Assert.That(host.Context.Label, Is.EqualTo(label));
            Assert.That(host.Context.Attempt, Is.EqualTo(attempts));
            Assert.That(host.TestRun, Is.SameAs(Get(label)));
            if (++attempts == 1) Assert.Fail("native retry");
            Assert.That(host.Context, Is.Not.SameAs(original));
        }).Then(() => Complete()).BDTestAsync();
    }

    internal static async Task Fail(AbstractContextBDTestBase<NUnitNativeContext> host, string mode)
    {
        await host.When(() => Complete()).Then(() => Complete()).BDTestAsync();
        if (mode.StartsWith("body")) throw new InvalidOperationException("native body");
    }

    internal static async Task Teardown(string label)
    {
        await Task.Yield();
        Console.WriteLine(label + " teardown");
        if (TestContext.CurrentContext.Test.Arguments.FirstOrDefault() is "teardown")
            throw new InvalidOperationException("native teardown");
    }
    private static void Complete() { }
}

public sealed class NUnitNativeContext : IBDTestContextLifetime, IAsyncDisposable
{
    public string Label { get; set; }
    public int Attempt { get; set; }
    public bool Initialized { get; private set; }
    public bool FailDisposal { get; set; }
    public bool Disposed { get; private set; }
    public int Disposals { get; private set; }
    public NUnitNativeContext() => NUnitCompositionEvidence.Contexts.Add(this);
    public async ValueTask InitializeAsync() { await Task.Yield(); Initialized = true; }
    public async ValueTask DisposeAsync()
    {
        Disposals++;
        await Task.Yield();
        Disposed = true;
        if (FailDisposal) throw new InvalidOperationException("native dispose");
    }
}

public abstract class NUnitCompanyBase { }

[Explicit("Executed by NUnitCompositionTests through an embedded runner.")]
[BDTestFixture, TestFixture("first"), TestFixture("second")]
[Parallelizable(ParallelScope.All), BDTestTag("native-class")]
public class NUnitComposedRunProbe : NUnitCompanyBase, INUnitBDTest
{
    private readonly string _label;
    private readonly NUnitBDTestScenario<NUnitNativeContext> _host;
    public NUnitComposedRunProbe(string label)
    {
        _label = label;
        _host = NUnitBDTestScenario.For<NUnitNativeContext>(this, NUnitCompositionEvidence.Get(label));
    }
    BDTestBase INUnitBDTest.BDTestHost => _host;
    [SetUp] public Task Setup() => NUnitCompositionEvidence.Setup(_host, _label);
    [TearDown] public Task Teardown() => NUnitCompositionEvidence.Teardown(_label);
    [TestCase(1), TestCase(2), Repeat(2), BDTestRetry(1)]
    [ScenarioText("Native row metadata"), BDTestTag("native-row")]
    public Task Run(int row) => NUnitCompositionEvidence.Execute(_host, _label);
}

[Explicit("Executed by NUnitCompositionTests through an embedded runner.")]
[TestFixture("first"), TestFixture("second")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase), Parallelizable(ParallelScope.All), BDTestTag("native-class")]
public class NUnitInheritedRunProbe(string label) : NUnitBDTestBase<NUnitNativeContext>
{
    public override BDTestRun TestRun => NUnitCompositionEvidence.Get(label);
    [SetUp] public Task Prepare() => NUnitCompositionEvidence.Setup(this, label);
    [TearDown] public Task Teardown() => NUnitCompositionEvidence.Teardown(label);
    [TestCase(1), TestCase(2), Repeat(2), BDTestRetry(1)]
    [ScenarioText("Native row metadata"), BDTestTag("native-row")]
    public Task Run(int row) => NUnitCompositionEvidence.Execute(this, label);
}

[Explicit("Executed by NUnitCompositionTests through an embedded runner.")]
[BDTestFixture]
public class NUnitComposedFailureProbe : NUnitCompanyBase, INUnitBDTest
{
    private readonly NUnitBDTestScenario<NUnitNativeContext> _host;
    public NUnitComposedFailureProbe() => _host = NUnitBDTestScenario.For<NUnitNativeContext>(this, NUnitCompositionEvidence.Get("failure"));
    BDTestBase INUnitBDTest.BDTestHost => _host;
    [SetUp] public Task Setup() => NUnitCompositionEvidence.Setup(_host, "failure");
    [TearDown] public Task Teardown() => NUnitCompositionEvidence.Teardown("failure");
    [TestCase("setup"), TestCase("body"), TestCase("teardown"), TestCase("dispose"), TestCase("body-dispose")]
    public Task Run(string mode) => NUnitCompositionEvidence.Fail(_host, mode);
}

[Explicit("Executed by NUnitCompositionTests through an embedded runner.")]
public class NUnitInheritedFailureProbe : NUnitBDTestBase<NUnitNativeContext>
{
    public override BDTestRun TestRun => NUnitCompositionEvidence.Get("failure");
    [SetUp] public Task Prepare() => NUnitCompositionEvidence.Setup(this, "failure");
    [TearDown] public Task Teardown() => NUnitCompositionEvidence.Teardown("failure");
    [TestCase("setup"), TestCase("body"), TestCase("teardown"), TestCase("dispose"), TestCase("body-dispose")]
    public Task Run(string mode) => NUnitCompositionEvidence.Fail(this, mode);
}
