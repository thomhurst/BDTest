using System;
using System.IO;
using System.Reflection;

namespace BDTest.Paths
{
    public static class FileLocations
    {
        public static readonly string OutputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static readonly string AggregatedJsonScenarios = Path.Combine(OutputDirectory, FileNames.TestDataJson);
        public static readonly string ScenariosDirectory = Path.Combine(OutputDirectory, FileNames.Scenarios);
        public static string RandomScenarioFilePath => Path.Combine(ScenariosDirectory, Guid.NewGuid() + ".json");
        public static readonly string Warnings = Path.Combine(OutputDirectory, FileNames.Warnings);

        public static readonly string HtmlReportWithStoriesFilePath = Path.Combine(OutputDirectory, FileNames.ReportByStory);
        public static readonly string HtmlReportWithoutStoriesFilePath = Path.Combine(OutputDirectory, FileNames.ReportAllScenarios);
        public static readonly string ProjectDirectory = Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.Parent?.FullName;
    }

    public static class FileNames
    {
        public static readonly string TimeStamp = DateTime.Now.ToString("yyyyMMdd HH-mm-ss-fff");
        public static readonly string TestDataJson = $"BDTest - Test Data - {TimeStamp}.json";
        public static readonly string TestDataXml = $"BDTest - Test Data - {TimeStamp}.xml";
        public static readonly string ReportByStory = $"BDTest - Report - By Story - {TimeStamp}.html";
        public static readonly string ReportAllScenarios = $"BDTest - Report - All Scenarios - {TimeStamp}.html";
        public static readonly string ReportFlakiness = $"BDTest - Report - Flakiness - {TimeStamp}.html";
        public static readonly string ReportTestTimesComparison = $"BDTest - Report - Test Times Comparison - {TimeStamp}.html";
        public static readonly string Scenarios = "BDTest - Scenarios";
        public static readonly string Warnings = "BDTest - Warnings.json";
    }
}
