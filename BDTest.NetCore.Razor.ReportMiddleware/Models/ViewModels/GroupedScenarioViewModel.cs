using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels
{
    public class GroupedScenarioViewModel
    {
        public List<Scenario> Scenarios { get; set; }
        public ReferenceInt ScenarioIndex { get; set; }
        public ReferenceInt GroupedScenarioIndex { get; set; }
    }
}