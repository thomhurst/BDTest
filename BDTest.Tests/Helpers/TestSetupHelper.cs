using BDTest.Maps;
using BDTest.Settings;

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