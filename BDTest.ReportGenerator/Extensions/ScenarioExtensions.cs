using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

namespace BDTest.ReportGenerator.Extensions
{
    public static class ScenarioExtensions
    {
        public static Status GetTotalStatus(this IEnumerable<Scenario> scenarios)
        {
            var scenariosList = scenarios.ToList();
            var firstScenario = scenariosList.FirstOrDefault(scenario => scenario.Status == Status.Failed)
                           ?? scenariosList.FirstOrDefault(scenario => scenario.Status == Status.NotImplemented)
                           ?? scenariosList.FirstOrDefault(scenario => scenario.Status == Status.Inconclusive)
                           ?? scenariosList.FirstOrDefault(scenario => scenario.Status == Status.Passed);

            var status = firstScenario?.Status ?? Status.Inconclusive;
            
            return status;
        }
    }
}