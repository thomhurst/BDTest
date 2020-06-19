using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;

namespace BDTest
{
    public static class BDTestUtil
    {
        public static List<Scenario> GetScenarios() => TestHolder.Scenarios.Values.ToList();
        public static string GetInstanceGuid => TestHolder.InstanceGuid; 
        public static List<BuildableTest> GetNotRunScenarios() => TestHolder.NotRun.Values.ToList();
        public static void ClearScenarios() => TestHolder.Scenarios.Clear();
        
        public static TestTimer GetTestTimer(IReadOnlyCollection<Scenario> scenarios)
        {
            if (scenarios.Count == 0)
            {
                return new TestTimer();
            }

            var testTimer = new TestTimer
            {
                TestsStartedAt = scenarios.GetStartDateTime(),
                TestsFinishedAt = scenarios.GetEndDateTime()
            };

            return testTimer;
        }
        
        private static DateTime GetStartDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderBy(scenario => scenario.StartTime).First().StartTime;
        }

        private static DateTime GetEndDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderByDescending(scenario => scenario.EndTime).First().EndTime;
        }
    }
}