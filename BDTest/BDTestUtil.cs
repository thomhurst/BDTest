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
            return GetTestTimer(scenarios, null);
        }

        public static TestTimer GetTestTimer(IReadOnlyCollection<Scenario> scenarios,
            BDTestOutputModel totalReportData)
        {
            return scenarios.Count switch
            {
                0 when totalReportData != null => new TestTimer
                {
                    TestsStartedAt = totalReportData.TestTimer.TestsStartedAt,
                    TestsFinishedAt = totalReportData.TestTimer.TestsFinishedAt
                },
                0 => new TestTimer(),
                _ => new TestTimer
                {
                    TestsStartedAt = scenarios.GetStartDateTime(), TestsFinishedAt = scenarios.GetEndDateTime()
                }
            };
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