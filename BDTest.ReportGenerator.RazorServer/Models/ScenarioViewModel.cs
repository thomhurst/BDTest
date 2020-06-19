using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Models
{
    public class ScenarioViewModel
    {
        public Scenario Scenario { get; set; }
        public ReferenceInt GroupedScenarioParentIndex { get; set; }
        public bool IsPartOfGroupedScenarios { get; set; }
        public ReferenceInt IndexOfScenarioInParent { get; set; }
    }
}