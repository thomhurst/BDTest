using System.Collections.Concurrent;
using Xunit;
using Xunit.Abstractions;
using BDTest.TestUtilities;

namespace BDTest.xUnit.Tests;

public class LifecycleTests : XUnitBDTestBase<XUnitContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();
    private readonly ITestOutputHelper _output;

    public LifecycleTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        Console.WriteLine("startup:" + BDTestExecutionId);
    }

    // Non-serializable data forces v2 to execute rows from one shared test case.
    public static IEnumerable<object[]> Rows => new[] { new object[] { new object() }, new object[] { new object() } };

    [Theory]
    [MemberData(nameof(Rows))]
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

    [Fact]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run, _output).Actions);

    [Fact]
    public Task LegacyIdsKeepCaptureAndCleanupTogether() =>
        LifecycleContract.VerifyLegacy(run => new LegacyProbe(run, _output).Actions);

    [Fact]
    public async Task RunAssignedInConstructorOwnsConstructorOutputAndCompletion()
    {
        using var run = BDTestRun.Start();
        var fixture = new ConstructorProbe(run, _output);
        await ((IAsyncLifetime)fixture).InitializeAsync();
        var originalContext = fixture.Context;
        var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.Single(run.Scenarios);
        Assert.Contains("before assigning owned run", scenario.TestStartupInformation);
        Assert.Contains("after assigning owned run", scenario.TestStartupInformation);
        await fixture.DisposeAsync();
        Assert.NotSame(originalContext, fixture.Context);
    }

    private sealed class ConstructorProbe : XUnitBDTestBase<XUnitContext>
    {
        private readonly BDTestRun _run;
        public override BDTestRun TestRun => _run;

        public ConstructorProbe(BDTestRun run, ITestOutputHelper output) : base(output)
        {
            Console.WriteLine("before assigning owned run");
            _run = run;
            Console.WriteLine("after assigning owned run");
        }
    }

    private sealed class LegacyProbe(BDTestRun run, ITestOutputHelper output) : XUnitBDTestBase<XUnitContext>(output)
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "legacy-id";
        public LifecycleContract.Probe Actions => new(this, _ => ((IAsyncLifetime)this).InitializeAsync().GetAwaiter().GetResult(), Dispose, () => Context);
    }

    private sealed class Probe(BDTestRun run, ITestOutputHelper output) : XUnitBDTestBase<XUnitContext>(output)
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}

public class AsyncLifecycleTests : XUnitBDTestBase<XUnitContext>
{
    private readonly BDTestRun _run;
    public override BDTestRun TestRun => _run;

    public AsyncLifecycleTests(ITestOutputHelper output) : base(output)
    {
        _run = BDTestRun.Start();
        Console.WriteLine("owned constructor");
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Task.Yield();
        Console.WriteLine("owned async setup");
    }

    [Fact]
    public async Task AsyncSetupAndBodyUseOwnedCapture()
    {
        Console.WriteLine("owned body startup");
        var scenario = await When(() => Console.WriteLine("owned step"))
            .Then(() => GC.KeepAlive(this)).BDTestAsync();
        Assert.Contains("owned constructor", scenario.TestStartupInformation);
        Assert.Contains("owned async setup", scenario.TestStartupInformation);
        Assert.Contains("owned body startup", scenario.TestStartupInformation);
        Assert.Contains("owned step", scenario.Steps.First().Output);
        Assert.Single(_run.Scenarios);
    }

    public override async Task DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _run.Dispose(); }
    }
}
