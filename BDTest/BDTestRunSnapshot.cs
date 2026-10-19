using BDTest.Contracts;

namespace BDTest;

/// <summary>Immutable execution results and timing copied from one run, without export metadata or I/O.</summary>
public sealed class BDTestRunSnapshot
{
    internal BDTestRunSnapshot(string reportId, string version, IReadOnlyList<ScenarioResult> scenarios,
        IReadOnlyList<ScenarioDefinitionSummary> notRun, ReportTimer timer)
    {
        ReportId = reportId;
        Version = version;
        Scenarios = scenarios;
        NotRun = notRun;
        Timer = timer;
    }

    public string ReportId { get; }
    public string Version { get; }
    public IReadOnlyList<ScenarioResult> Scenarios { get; }
    public IReadOnlyList<ScenarioDefinitionSummary> NotRun { get; }
    public ReportTimer Timer { get; }
}
