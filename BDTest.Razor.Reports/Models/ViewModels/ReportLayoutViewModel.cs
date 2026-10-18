using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class ReportLayoutViewModel
{
    private List<Scenario> _scenarios;
    private Dictionary<Status, int> _counts;
    private TestTimer _timer;
    private int? _failureCauses;

    public BDTestOutputModel Report { get; set; }

    public IReadOnlyList<Scenario> Scenarios =>
        _scenarios ??= Report?.Scenarios?.Where(scenario => scenario != null).ToList() ?? new List<Scenario>();

    /// <summary>Number of scenarios with each status. Every status is present.</summary>
    public IReadOnlyDictionary<Status, int> Counts => _counts ??= Enum.GetValues<Status>()
        .ToDictionary(status => status, status => Scenarios.Count(scenario => scenario.Status == status));

    public Status Status => Scenarios.GetTotalStatus();

    public TestTimer Timer => _timer ??= Report == null ? new TestTimer() : BDTestUtil.GetTestTimer(Scenarios.ToList(), Report.TestTimer == null ? null : Report);

    /// <summary>The run's display name: its tag, or a generic name for untagged runs.</summary>
    public string Title => string.IsNullOrWhiteSpace(Report?.Tag) ? "Test run" : Report.Tag;

    public int FailureCauses => _failureCauses ??= FailureGroup.Build(Scenarios, FailureGroup.ByExceptionAndLocation).Count;

    public int NotRunCount => Report?.NotRun?.Count ?? 0;
}
