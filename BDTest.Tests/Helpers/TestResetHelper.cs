using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.Tests.Helpers
{
    public static class TestResetHelper
    {
        public static List<Scenario> ClearedScenarios { get; } = new List<Scenario>();
        public static void ResetData()
        {
            ClearedScenarios.AddRange(BDTestUtil.GetScenarios());
            BDTestUtil.ClearScenarios();
        }
    }
}