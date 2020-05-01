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
            BDTestSettings.AllScenariosReportHtmlFilename = "BDTest - All Scenarios.html";
            BDTestSettings.ScenariosByStoryReportHtmlFilename = "BDTest - Stories.html";
            BDTestSettings.ReportFolderName = "BDTestOutput";
        }

        [OneTimeTearDown]
        public void GenerateReport()
        {
            BDTestReportGenerator.Generate();
        }
    }
}