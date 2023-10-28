using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class GroupedScenarioViewModel
{
    public List<Scenario> Scenarios { get; set; }
    public ReferenceInt ScenarioIndex { get; set; }
    public ReferenceInt GroupedScenarioIndex { get; set; }
}