using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
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

        public static string GetCssColourValueForStatus(this Status status)
        {
            switch (status)
            {
                case Status.Passed:
                    return "btn-success";
                case Status.Failed:
                    return "btn-danger";
                case Status.Inconclusive:
                case Status.NotImplemented:
                case Status.Skipped:
                    return "btn-warning";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        public static string AsString(this Status status)
        {
            switch (status)
            {
                case Status.Passed:
                case Status.Failed:
                case Status.Inconclusive:
                case Status.Skipped:
                    return status.ToString();
                case Status.NotImplemented:
                    return "Not Implemented";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}