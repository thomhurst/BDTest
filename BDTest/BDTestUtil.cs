using System.Collections.Generic;
using System.Linq;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest
{
    public static class BDTestUtil
    {
        public static List<Scenario> GetScenarios() => TestHolder.Scenarios.Values.ToList();
        public static List<BuildableTest> GetNotRunScenarios() => TestHolder.NotRun.Values.ToList();
        public static void ClearScenarios() => TestHolder.Scenarios.Clear();
    }
}