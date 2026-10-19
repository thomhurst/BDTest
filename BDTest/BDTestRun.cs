using System.Text;
using BDTest.Engine;
using BDTest.Interfaces.Internal;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest;

/// <summary>Owns the report, scenario registry, executor, and output buffers for one test run.</summary>
/// <remarks>
/// Override BDTestBase.TestRun to select this run in a custom adapter. Dispose only after its tests
/// and output writers have completed. Runs freeze their configuration at Start.
/// </remarks>
public sealed class BDTestRun : IDisposable
{
    internal TestRunStore Store { get; } = new();
    internal Dictionary<string, StringBuilder> OutputBuffers { get; } = new();
    internal IScenarioExecutor ScenarioExecutor { get; } = new ScenarioExecutor(new ScenarioRetryManager());
    internal RunOptions Options { get; }
    internal DateTime StartedAt { get; }
    internal AttachmentFiles AttachmentFiles { get; }

    private BDTestRun(BDTestOptions options)
    {
        Options = new RunOptions(options);
        StartedAt = Options.Clock.UtcNow;
        AttachmentFiles = new AttachmentFiles(options.Attachments);
    }

    /// <summary>Gets the unique report identifier created for this run.</summary>
    public string ReportId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets a snapshot of top-level scenarios recorded by this run.</summary>
    public IReadOnlyCollection<Contracts.ScenarioResult> Scenarios => BDTestUtil.GetScenarios(this);

    /// <summary>Starts an empty run with a new report identifier.</summary>
    public static BDTestRun Start() => new(new BDTestOptions());

    /// <summary>Starts a run with an immutable snapshot of the supplied registrations.</summary>
    /// <remarks>Later registration changes do not affect this run. Callback instances are not cloned.</remarks>
    public static BDTestRun Start(BDTestOptions options) => new(options ?? throw new ArgumentNullException(nameof(options)));

    /// <summary>Copies this run's results, pending definitions and timing into an immutable in-memory snapshot.</summary>
    /// <remarks>Throws after disposal. An empty run spans its start through the current run clock.</remarks>
    public BDTestRunSnapshot GetSnapshot()
    {
        lock (Store.Gate)
        {
            ThrowIfDisposed();
            var scenarios = BDTestUtil.GetScenarios(this);
            var timer = scenarios.Count == 0
                ? new Contracts.ReportTimer(Helpers.ResultSnapshot.Utc(StartedAt), Helpers.ResultSnapshot.Utc(Options.Clock.UtcNow))
                : BDTestUtil.GetTestTimer(scenarios);
            return new BDTestRunSnapshot(ReportId, Helpers.BDTestVersionHelper.CurrentVersion ?? "unknown",
                scenarios, BDTestUtil.GetNotRunScenarios(this), timer);
        }
    }

    internal void ThrowIfDisposed() => Store.ThrowIfDisposed();

    /// <summary>Opens captured content owned by this run. The caller must dispose the stream.</summary>
    /// <remarks>Throws if this run is disposed, or the attachment is unknown or has been cleared.</remarks>
    public Stream OpenAttachment(string attachmentId)
    {
        if (attachmentId == null) throw new ArgumentNullException(nameof(attachmentId));
        lock (Store.Gate)
        {
            ThrowIfDisposed();
            return AttachmentFiles.Open(attachmentId);
        }
    }

    internal void Clear()
    {
        lock (Store.Gate)
        {
            AttachmentFiles.Clear();
            OutputBuffers.Clear();
            Store.Clear();
        }
    }

    /// <summary>Clears stores, deletes temporary attachment files and prevents new scenario construction or execution.</summary>
    /// <remarks>Does not cancel tests in progress. Call after all tests, output writers and attachment readers finish.</remarks>
    public void Dispose()
    {
        lock (Store.Gate)
        {
            Clear();
            Store.IsDisposed = true;
        }
    }
}
