using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Models
{
    public class ScenarioModalViewModel
    {
        public Scenario Scenario { get; set; }
        public Scenario LastScenario { get; set; }
        public Scenario NextScenario { get; set; }
    }
}