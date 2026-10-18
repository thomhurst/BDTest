using BDTest.Test;
using Xunit;

namespace BDTest.xUnit.Tests;

// Fixture disposal happens after xUnit disposes the test instance. Missing or duplicate
// adapter cleanup must fail the runner, even when the test body itself passes.
public sealed class CleanupProbe : IDisposable
{
    public int CompletionCount { get; set; }
    public bool AsyncTeardownFinished { get; set; }
    public bool RequiresAsyncTeardown { get; set; }
    public bool RequiresSyncTeardown { get; set; }
    public bool SyncTeardownFinished { get; set; }

    public void Dispose()
    {
        Assert.Equal(1, CompletionCount);
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
        Assert.Equal(0, Probe.CompletionCount);
        await When(() => Assert.NotNull(Context))
            .Then(() => Assert.Equal(0, Probe.CompletionCount)).BDTestAsync();
    }

    protected override void MarkTestAsComplete()
    {
        base.MarkTestAsComplete();
        Probe.CompletionCount++;
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

public sealed class XUnitAsyncLifetimeCleanupTests(CleanupProbe probe) : XUnitCleanupTests(probe),
    IClassFixture<CleanupProbe>, IAsyncLifetime
{
    private bool _initialized;

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        _initialized = true;
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
            await base.DisposeAsync();
        }
    }
}
