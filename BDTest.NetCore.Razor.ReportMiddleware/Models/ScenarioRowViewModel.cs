using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class ScenarioRowViewModel
    {
        public Scenario Scenario { get; set; }
        public ReferenceInt GroupedScenarioParentIndex { get; set; }
        public bool IsPartOfGroupedScenarios { get; set; }
        public ReferenceInt IndexOfScenarioInParent { get; set; }
    }
}