using BDTest.Settings;

namespace BDTest.Tests.Helpers
{
    public class TestSetupHelper
    {
        public static void ResetData()
        {
            BDTestSettings.LegacyReportSettings.ReportFolderName = "CustomFolder";
            
            BDTestUtil.ClearScenarios();
        }
    }
}