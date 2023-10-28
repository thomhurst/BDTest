using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class ScenarioModalViewModel
{
    public Scenario Scenario { get; set; }
    public Scenario LastScenario { get; set; }
    public Scenario NextScenario { get; set; }
}