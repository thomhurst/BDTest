using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class ScenarioRowViewModel
{
    public Scenario Scenario { get; set; }
    public ReferenceInt GroupedScenarioParentIndex { get; set; }
    public bool IsPartOfGroupedScenarios { get; set; }
    public ReferenceInt IndexOfScenarioInParent { get; set; }
}