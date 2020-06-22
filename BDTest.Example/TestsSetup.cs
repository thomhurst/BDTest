using BDTest.ReportGenerator;
using BDTest.Settings;
using NUnit.Framework;

namespace BDTest.Example
{
    [SetUpFixture]
    public class TestsSetup
    {
        [OneTimeSetUp]
        public void SetupBDTestSettings()
        {
            BDTestSettings.LegacyReportSettings.AllScenariosReportHtmlFilename = "BDTest - All Scenarios.html";
            BDTestSettings.LegacyReportSettings.ScenariosByStoryReportHtmlFilename = "BDTest - Stories.html";
            BDTestSettings.LegacyReportSettings.ReportFolderName = "BDTestOutput";
        }

        [OneTimeTearDown]
        public void GenerateReport()
        {
            BDTestReportGenerator.Generate();
        }
    }
}