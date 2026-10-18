using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

/// <summary>The Scenarios page: filtered scenarios, how they are grouped and the selected scenario.</summary>
public class ScenarioListViewModel
{
    public const string GroupByStory = "story";
    public const string GroupByNone = "none";

    public string? ReportId { get; set; }

    /// <summary>Scenarios that match every filter, in display order.</summary>
    public IReadOnlyList<Scenario> Scenarios { get; set; } = new List<Scenario>();

    /// <summary>Scenarios that match the tag and search filters, before the status filter. Used for the status counts.</summary>
    public IReadOnlyList<Scenario> Unfiltered { get; set; } = new List<Scenario>();

    /// <summary>Number of scenarios in the run.</summary>
    public int Total { get; set; }

    public string GroupBy { get; set; } = GroupByStory;

    /// <summary>True on the older Stories route, which always groups by story.</summary>
    public bool GroupFixed { get; set; }

    /// <summary>Selected statuses, or null for every status.</summary>
    public IReadOnlySet<Status>? Statuses { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = new List<string>();

    public Scenario? Selected { get; set; }
}

public class ScenarioToolbarViewModel
{
    public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();
    public string GroupBy { get; set; } = ScenarioListViewModel.GroupByStory;
    public bool GroupFixed { get; set; }
}
