using System.Text;
using BDTest.Engine;
using BDTest.Interfaces.Internal;
using BDTest.Maps;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest;

/// <summary>Owns the report, scenario registry, executor, and output buffers for one test run.</summary>
/// <remarks>
/// Override BDTestBase.TestRun to select this run in a custom adapter. Dispose only after its tests
/// and output writers have completed. Explicit runs freeze their configuration at Start; the legacy
/// default run continues to use BDTestSettings.
/// </remarks>
public sealed class BDTestRun : IDisposable
{
    internal static BDTestRun Default { get; } = new();
    internal TestRunStore Store { get; } = new();
    internal Dictionary<string, StringBuilder> OutputBuffers { get; } = new();
    internal IScenarioExecutor ScenarioExecutor { get; } = new ScenarioExecutor(new ScenarioRetryManager());
    internal RunOptions Options { get; }
    internal DateTime StartedAt { get; } = DateTime.UtcNow;

    private BDTestRun() => Options = RunOptions.Legacy;

    private BDTestRun(BDTestOptions options) => Options = new RunOptions(options);

    /// <summary>Gets the unique report identifier created for this run.</summary>
    public string ReportId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets a snapshot of top-level scenarios recorded by this run.</summary>
    public IReadOnlyCollection<Scenario> Scenarios => Store.ScenariosByInternalId.Values
        .Where(scenario => scenario.OutlineId == null).ToArray();

    /// <summary>Starts an empty run with a new report identifier.</summary>
    public static BDTestRun Start() => new(new BDTestOptions());

    /// <summary>Starts a run with an immutable snapshot of the supplied registrations.</summary>
    /// <remarks>Later registration changes do not affect this run. Callback instances are not cloned.</remarks>
    public static BDTestRun Start(BDTestOptions options) => new(options ?? throw new ArgumentNullException(nameof(options)));

    internal void ThrowIfDisposed() => Store.ThrowIfDisposed();

    internal void Clear()
    {
        lock (Store.Gate)
        {
            OutputBuffers.Clear();
            Store.Clear();
        }
    }

    /// <summary>Clears this run's stores and prevents new scenario construction or execution.</summary>
    /// <remarks>Does not cancel tests in progress. Call after all tests and output writers finish.</remarks>
    public void Dispose()
    {
        lock (Store.Gate)
        {
            Clear();
            Store.IsDisposed = true;
        }
    }
}
