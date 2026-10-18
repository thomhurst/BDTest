using System.Collections.Concurrent;
using Xunit;
using BDTest.TestUtilities;

namespace BDTest.xUnit.Tests;

public class LifecycleTests : XUnitBDTestBase<XUnitContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();

    public LifecycleTests()
    {
        Console.WriteLine("startup:" + BDTestExecutionId);
    }

    // Exercise constructor capture for runtime-enumerated, non-serializable rows.
    public static IEnumerable<object[]> Rows => new[] { new object[] { new object() }, new object[] { new object() } };

    [Theory]
    [MemberData(nameof(Rows), DisableDiscoveryEnumeration = true)]
    public async Task NonSerializableRowsHaveUniqueIdsAndCaptureConstructorOutput(object row)
    {
        var id = BDTestExecutionId;
        var context = Context;
        Assert.True(Ids.TryAdd(id, 0), $"Duplicate execution ID: {id}");
        await Task.Yield();
        Assert.Equal(id, BDTestExecutionId);
        Assert.Same(context, Context);
        var scenario = await When(() => GC.KeepAlive(row)).Then(() => NoOp()).BDTestAsync();
        Assert.Equal(id, scenario.FrameworkTestId);
        Assert.Contains("startup:" + id, scenario.TestStartupInformation);
    }

    private static void NoOp() { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalPreservesLegacyCompletionOverride(bool asynchronously)
    {
        using var run = BDTestRun.Start();
        var fixture = new CompletionHookProbe(run);
        await ((IAsyncLifetime)fixture).InitializeAsync();
        var originalContext = fixture.Context;

        if (asynchronously)
            await fixture.DisposeAsync();
        else
            fixture.Dispose();

        Assert.Equal(1, fixture.CompletionCalls);
        Assert.NotSame(originalContext, fixture.Context);
    }

    private sealed class CompletionHookProbe(BDTestRun run) : XUnitBDTestBase<XUnitContext>
    {
        public override BDTestRun TestRun => run;
        public int CompletionCalls { get; private set; }

#pragma warning disable CS0672, CS0618 // Exercise the retained compatibility hook.
        protected override void MarkTestAsComplete()
        {
            CompletionCalls++;
            base.MarkTestAsComplete();
        }
#pragma warning restore CS0672, CS0618
    }

    [Fact]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run).Actions);

    [Fact]
    public Task LegacyIdsKeepCaptureAndCleanupTogether() =>
        LifecycleContract.VerifyLegacy(run => new LegacyProbe(run).Actions);

    [Fact]
    public async Task RunAssignedInConstructorOwnsConstructorOutputAndCompletion()
    {
        using var run = BDTestRun.Start();
        var fixture = new ConstructorProbe(run);
        await ((IAsyncLifetime)fixture).InitializeAsync();
        var originalContext = fixture.Context;
        var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.Single(run.Scenarios);
        Assert.Contains("before assigning owned run", scenario.TestStartupInformation);
        Assert.Contains("after assigning owned run", scenario.TestStartupInformation);
        await fixture.DisposeAsync();
        Assert.NotSame(originalContext, fixture.Context);
    }

    [Fact]
    public async Task FailedReimplementedSetupStillCleansContextAndPendingOutput()
    {
        using var run = BDTestRun.Start();
        var fixture = new FailedSetupProbe(run);
        var originalContext = fixture.Context;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IAsyncLifetime)fixture).InitializeAsync().AsTask());
        await fixture.DisposeAsync();
        Assert.NotSame(originalContext, fixture.Context);

        var next = new Probe(run);
        next.Actions.Begin("ignored-runner-id");
        var scenario = await next.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.True(string.IsNullOrEmpty(scenario.TestStartupInformation));
        next.Actions.End();
    }

    private sealed class FailedSetupProbe(BDTestRun run) : XUnitBDTestBase<XUnitContext>, IAsyncLifetime
    {
        public override BDTestRun TestRun => run;

        ValueTask IAsyncLifetime.InitializeAsync()
        {
            Console.WriteLine("failed setup output");
            throw new InvalidOperationException("setup failed");
        }
    }

    private sealed class ConstructorProbe : XUnitBDTestBase<XUnitContext>
    {
        private readonly BDTestRun _run;
        public override BDTestRun TestRun => _run;

        public ConstructorProbe(BDTestRun run)
        {
            Console.WriteLine("before assigning owned run");
            _run = run;
            Console.WriteLine("after assigning owned run");
        }
    }

    private sealed class LegacyProbe(BDTestRun run) : XUnitBDTestBase<XUnitContext>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "legacy-id";
        public LifecycleContract.Probe Actions => new(this, _ => ((IAsyncLifetime)this).InitializeAsync().AsTask().GetAwaiter().GetResult(), Dispose, () => Context);
    }

    private sealed class Probe(BDTestRun run) : XUnitBDTestBase<XUnitContext>
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}

public class AsyncLifecycleTests : XUnitBDTestBase<XUnitContext>
{
    private readonly TaskCompletionSource _releaseOutput = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _constructorTask;
    private readonly BDTestRun _run;
    public override BDTestRun TestRun => _run;

    public AsyncLifecycleTests()
    {
        _run = BDTestRun.Start();
        Console.WriteLine("owned constructor");
        _constructorTask = Task.Run(async () =>
        {
            await _releaseOutput.Task;
            Console.Write('!');
            Console.WriteLine("constructor task output");
        });
    }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await Task.Yield();
        Console.WriteLine("owned async setup");
        _releaseOutput.SetResult();
        await _constructorTask.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task AsyncSetupAndBodyUseOwnedCapture()
    {
        Console.WriteLine("owned body startup");
        var scenario = await When(() => Console.WriteLine("owned step"))
            .Then(() => GC.KeepAlive(this)).BDTestAsync();
        Assert.Contains("owned constructor", scenario.TestStartupInformation);
        Assert.Contains("owned async setup", scenario.TestStartupInformation);
        Assert.Contains("!constructor task output", scenario.TestStartupInformation);
        Assert.Contains("owned body startup", scenario.TestStartupInformation);
        Assert.Contains("owned step", scenario.Steps.First().Output);
        Assert.Single(_run.Scenarios);
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _run.Dispose(); }
    }
}
