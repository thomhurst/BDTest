using System.Collections.ObjectModel;
using BDTest.Contracts;

namespace BDTest.Rendering;

/// <summary>Immutable run display data that never retains scenarios or their diagnostic graphs.</summary>
public sealed class ReportRunOverview
{
    public int ScenarioCount { get; }
    public int NotRunCount { get; }
    public IReadOnlyDictionary<Status, int> Counts { get; }
    public string? Environment { get; }
    public string? Tag { get; }
    public string? BranchName { get; }
    public string? MachineName { get; }
    public string? ProducerVersion { get; }
    public ReportTimer? Timer { get; }
    public IReadOnlyDictionary<string, string> CustomProperties { get; }

    public ReportRunOverview(int scenarioCount, int notRunCount, IReadOnlyDictionary<Status, int> counts,
        string? environment = null, string? tag = null, string? branchName = null, string? machineName = null,
        string? producerVersion = null, ReportTimer? timer = null, IReadOnlyDictionary<string, string>? customProperties = null)
    {
        if (scenarioCount < 0) throw new ArgumentOutOfRangeException(nameof(scenarioCount));
        if (notRunCount < 0) throw new ArgumentOutOfRangeException(nameof(notRunCount));
        if (counts == null) throw new ArgumentNullException(nameof(counts));
        var copy = new Dictionary<Status, int>();
        foreach (Status status in Enum.GetValues(typeof(Status)))
        {
            var count = counts.TryGetValue(status, out var value) ? value : 0;
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(counts));
            copy.Add(status, count);
        }
        ScenarioCount = scenarioCount;
        NotRunCount = notRunCount;
        Counts = new ReadOnlyDictionary<Status, int>(copy);
        Environment = environment;
        Tag = tag;
        BranchName = branchName;
        MachineName = machineName;
        ProducerVersion = producerVersion;
        Timer = timer;
        var properties = new Dictionary<string, string>();
        if (customProperties != null)
            foreach (var pair in customProperties) properties.Add(pair.Key, pair.Value);
        CustomProperties = new ReadOnlyDictionary<string, string>(properties);
    }

    /// <summary>Projects a report without retaining its scenarios, output, attachments or exceptions.</summary>
    public static ReportRunOverview FromReport(BDTestReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var counts = new Dictionary<Status, int>();
        foreach (var scenario in report.Scenarios)
        {
            counts.TryGetValue(scenario.Status, out var count);
            counts[scenario.Status] = count + 1;
        }
        return new ReportRunOverview(report.Scenarios.Count, report.NotRun.Count, counts, report.Environment,
            report.Tag, report.BranchName, report.MachineName, report.ProducerVersion, report.TestTimer, report.CustomProperties);
    }
}
