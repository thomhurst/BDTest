using BDTest.Maps;

namespace BDTest.Tests.Helpers
{
    public class TestSetupHelper
    {
        public static void ResetData()
        {
            BDTestSettings.ReportFolderName = "CustomFolder";

            TestHolder.Scenarios.Clear();
        }
    }
}