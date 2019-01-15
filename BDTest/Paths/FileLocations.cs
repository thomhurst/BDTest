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
        public static readonly string TestDataJson = $"test_data - {TimeStamp}.json";
        public static readonly string TestDataXml = $"test_data - {TimeStamp}.xml";
        public static readonly string ReportByStory = $"Report - By Story - {TimeStamp}.html";
        public static readonly string ReportAllScenarios = $"Report - All Scenarios - {TimeStamp}.html";
        public static readonly string Scenarios = "Scenarios";
        public static readonly string Warnings = "Warnings.json";
    }
}
