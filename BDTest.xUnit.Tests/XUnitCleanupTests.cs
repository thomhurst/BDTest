using BDTest.Test;
using Xunit;

namespace BDTest.xUnit.Tests;

// Fixture disposal happens after xUnit disposes the test instance. Missing
// adapter cleanup must fail the runner, even when the test body itself passes.
public sealed class CleanupProbe : IDisposable
{
    public Action VerifyCompletion { get; set; }
    public object SetupContext { get; set; }
    public string[] ExpectedStartupOutput { get; set; } = [];
    public bool AsyncTeardownFinished { get; set; }
    public bool RequiresAsyncTeardown { get; set; }
    public bool RequiresSyncTeardown { get; set; }
    public bool SyncTeardownFinished { get; set; }

    public void Dispose()
    {
        Assert.NotNull(VerifyCompletion);
        VerifyCompletion();
        if (RequiresAsyncTeardown)
        {
            Assert.True(AsyncTeardownFinished);
        }
        if (RequiresSyncTeardown)
        {
            Assert.True(SyncTeardownFinished);
        }
    }
}

public abstract class XUnitCleanupTests(CleanupProbe probe) : XUnitBDTestBase<object>
{
    protected CleanupProbe Probe { get; } = probe;

    [Fact]
    public async Task ScenarioRunsBeforeRunnerCleanup()
    {
        var originalContext = Context;
        if (Probe.SetupContext != null) Assert.Same(Probe.SetupContext, originalContext);
        var scenario = await When(() => Assert.Same(originalContext, Context))
            .Then(() => Assert.Same(originalContext, Context)).BDTestAsync();
        foreach (var expected in Probe.ExpectedStartupOutput)
            Assert.Contains(expected, scenario.TestStartupInformation);
        Probe.VerifyCompletion = () => Assert.NotSame(originalContext, Context);
    }
}

public sealed class XUnitDefaultCleanupTests(CleanupProbe probe) : XUnitCleanupTests(probe), IClassFixture<CleanupProbe>;

public sealed class XUnitSyncDisposableCleanupTests : XUnitCleanupTests, IClassFixture<CleanupProbe>, IDisposable
{
    public XUnitSyncDisposableCleanupTests(CleanupProbe probe) : base(probe)
    {
        Probe.RequiresSyncTeardown = true;
    }

    public new void Dispose()
    {
        try
        {
            Probe.SyncTeardownFinished = true;
        }
        finally
        {
            base.Dispose();
        }
    }
}

public sealed class XUnitAsyncDisposableCleanupTests(CleanupProbe probe) : XUnitCleanupTests(probe), IClassFixture<CleanupProbe>
{
    public override async ValueTask DisposeAsync()
    {
        try
        {
            Probe.RequiresAsyncTeardown = true;
            await Task.Yield();
            Probe.AsyncTeardownFinished = true;
        }
        finally
        {
            await base.DisposeAsync();
        }
    }
}

public sealed class XUnitAsyncLifetimeCleanupTests : XUnitCleanupTests,
    IClassFixture<CleanupProbe>, IAsyncLifetime
{
    private readonly BDTestRun _run;
    private bool _initialized;
    public override BDTestRun TestRun => _run;

    public XUnitAsyncLifetimeCleanupTests(CleanupProbe probe) : base(probe)
    {
        _run = BDTestRun.Start();
        Console.WriteLine("reimplemented constructor");
    }

    public new async ValueTask InitializeAsync()
    {
        await Task.Yield();
        _initialized = true;
        Probe.SetupContext = Context;
        Probe.ExpectedStartupOutput = ["reimplemented constructor", "reimplemented async setup"];
        Console.WriteLine("reimplemented async setup");
        Probe.RequiresAsyncTeardown = true;
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            Assert.True(_initialized);
            await Task.Yield();
            Probe.AsyncTeardownFinished = true;
        }
        finally
        {
            try { await base.DisposeAsync(); }
            finally { _run.Dispose(); }
        }
    }
}
