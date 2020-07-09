using System;
using System.Linq;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class MultipleTestRunsTimingDifferenceModel
    {
        public MultipleTestRunsTimingDifferenceModel(IGrouping<ScenarioGroupKey, Scenario> scenarioGroup)
        {
            var scenarios = scenarioGroup.Where(scenario => scenario.Status == Status.Passed).ToList();
            
            if (!scenarios.Any())
            {
                return;
            }

            ScenarioCount = scenarios.Count;
            ScenarioName = scenarios.First().GetScenarioText();
            Fastest = scenarios.OrderBy(scenario => scenario.TimeTaken).First().TimeTaken;
            Slowest = scenarios.OrderByDescending(scenario => scenario.TimeTaken).First().TimeTaken;
            Average = TimeSpan.FromMilliseconds(scenarios.Average(scenario => scenario.TimeTaken.TotalMilliseconds));
            Difference = Slowest - Fastest;
        }

        public int ScenarioCount { get; set; }

        public string ScenarioName { get; set; }

        public TimeSpan Difference { get; set; }

        public TimeSpan Average { get; set; }

        public TimeSpan Slowest { get; set; }

        public TimeSpan Fastest { get; set; }
    }

    public struct ScenarioGroupKey : IEquatable<ScenarioGroupKey>
    {
        public string ScenarioText { get; set; }
        public string StoryText { get; set; }

        public bool Equals(ScenarioGroupKey other)
        {
            return ScenarioText == other.ScenarioText && StoryText == other.StoryText;
        }

        public override bool Equals(object obj)
        {
            return obj is ScenarioGroupKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ScenarioText, StoryText);
        }
    }
}