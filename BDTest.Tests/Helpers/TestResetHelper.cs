using System.Collections.Generic;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Tests.Helpers
{
    public static class TestResetHelper
    {
        public static List<Scenario> ClearedScenarios { get; } = new List<Scenario>();
        public static void ResetData()
        {
            BDTestSettings.LegacyReportSettings.ReportFolderName = "CustomFolder";
            
            ClearedScenarios.AddRange(BDTestUtil.GetScenarios());
            BDTestUtil.ClearScenarios();
        }
    }
}