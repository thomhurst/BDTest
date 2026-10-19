using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

/// <summary>Display metadata only; never retains the report's scenario graph.</summary>
public sealed record ReportMetadataViewModel(string? Id, string? Tag, string? Environment,
    string? BranchName, string? MachineName, string? Version, IReadOnlyDictionary<string, string> CustomProperties,
    bool HasTimer);

public class ReportLayoutViewModel
{
    private Lazy<int>? _failureCauses;
    private int _failureCausesValue;

    public ReportMetadataViewModel? Report { get; init; }
    public int ScenarioCount { get; init; }
    public IReadOnlyDictionary<Status, int> Counts { get; init; } = Enum.GetValues<Status>().ToDictionary(status => status, _ => 0);
    [System.Text.Json.Serialization.JsonConverter(typeof(BDTest.Razor.Reports.Helpers.ReportStatusConverter))]
    public Status Status { get; init; } = Status.NotRun;
    public TestTimer Timer { get; init; } = new() { TestsStartedAt = default, TestsFinishedAt = default };
    public string Title => string.IsNullOrWhiteSpace(Report?.Tag) ? "Test run" : Report.Tag;
    public int FailureCauses
    {
        get => _failureCauses?.Value ?? _failureCausesValue;
        init => _failureCausesValue = value;
    }
    public int NotRunCount { get; init; }

    public static ReportLayoutViewModel Create(BDTestOutputModel report) => Create(report,
        (report.Scenarios ?? []).Where(scenario => scenario != null).ToList(), null);

    internal static ReportLayoutViewModel Create(BDTestOutputModel report, List<Scenario> scenarios, int? failureCauses)
    {
        var counts = Enum.GetValues<Status>().ToDictionary(status => status, _ => 0);
        foreach (var scenario in scenarios)
            if (counts.ContainsKey(scenario.Status)) counts[scenario.Status]++;
        return new ReportLayoutViewModel
        {
            Report = new(report.Id, report.Tag, report.Environment, report.BranchName, report.MachineName, report.Version,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(report.CustomProperties ?? [])),
                report.TestTimer != null),
            ScenarioCount = scenarios.Count,
            Counts = new System.Collections.ObjectModel.ReadOnlyDictionary<Status, int>(counts),
            Status = scenarios.GetTotalStatus(),
            Timer = BDTestUtil.GetTestTimer(scenarios, report.TestTimer == null ? null : report),
            FailureCauses = failureCauses.GetValueOrDefault(),
            // Only request-scoped full-data layouts retain scenarios for on-demand grouping.
            _failureCauses = failureCauses.HasValue ? null : new Lazy<int>(() =>
                FailureGroup.Build(scenarios, FailureGroup.ByExceptionAndLocation).Count),
            NotRunCount = report.NotRun?.Count ?? 0
        };
    }
}
