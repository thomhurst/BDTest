using BDTest.Contracts;

namespace BDTest.Razor.Reports.Models.ViewModels;

/// <summary>The scenario shown in the side panel of the Scenarios page.</summary>
public class ScenarioModalViewModel
{
    public ScenarioResult? Scenario { get; set; }
    public ScenarioResult? LastScenario { get; set; }
    public ScenarioResult? NextScenario { get; set; }

    /// <summary>One-based position of the scenario in the current list, or 0 when unknown.</summary>
    public int Position { get; set; }

    public int Count { get; set; }

    public string? PreviousUrl { get; set; }
    public string? NextUrl { get; set; }
    public string? CloseUrl { get; set; }
    public string? FullPageUrl { get; set; }
}
