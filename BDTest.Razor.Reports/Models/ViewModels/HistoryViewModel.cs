using BDTest.Contracts;

namespace BDTest.Razor.Reports.Models.ViewModels;

/// <summary>Filters and runs shared by the History tabs.</summary>
public class HistoryViewModel
{
    public const string Trends = "trends";
    public const string Flaky = "flaky";
    public const string Timing = "timing";

    public string Tab { get; set; } = Trends;

    public RunFilter Filter { get; set; } = new();

    /// <summary>Every stored run, used for the filter options.</summary>
    public IReadOnlyList<TestRunSummary> AllRuns { get; set; } = new List<TestRunSummary>();

    /// <summary>Runs that match the filters, oldest first.</summary>
    public IReadOnlyList<TestRunSummary> Runs { get; set; } = new List<TestRunSummary>();

    /// <summary>How many of the latest matching runs the Flaky and Timing tabs compare.</summary>
    public int Compare { get; set; } = 20;

    /// <summary>True when the reports were chosen by ID rather than by the filters.</summary>
    public bool ChosenRuns { get; set; }

    /// <summary>Loaded reports for the Flaky and Timing tabs, oldest first.</summary>
    public IReadOnlyList<BDTestReport> Reports { get; set; } = new List<BDTestReport>();
}
