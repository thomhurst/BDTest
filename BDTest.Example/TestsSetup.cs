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
            BDTestSettings.ReportSettings.AllScenariosReportHtmlFilename = "BDTest - All Scenarios.html";
            BDTestSettings.ReportSettings.ScenariosByStoryReportHtmlFilename = "BDTest - Stories.html";
            BDTestSettings.ReportSettings.ReportFolderName = "BDTestOutput";
        }

        [OneTimeTearDown]
        public void GenerateReport()
        {
            BDTestReportGenerator.Generate();
        }
    }
}