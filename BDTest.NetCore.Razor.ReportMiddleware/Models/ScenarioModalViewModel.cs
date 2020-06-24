using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class ScenarioModalViewModel
    {
        public Scenario Scenario { get; set; }
        public Scenario LastScenario { get; set; }
        public Scenario NextScenario { get; set; }
    }
}