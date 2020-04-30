using System.IO;
using BDTest.Maps;

namespace BDTest.Tests
{
    public class TestSetupHelper
    {
        public static void ResetData()
        {
            BDTestSettings.ReportFolderName = "CustomFolder";

            if (FileHelpers.HasCustomFolder() && File.Exists(FileHelpers.GetOutputFolder()))
            {
                Directory.Delete(FileHelpers.GetOutputFolder(), true);
            }
            
            TestHolder.Scenarios.Clear();
        }
    }
}