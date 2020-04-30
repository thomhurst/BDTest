using System;
using System.IO;
using System.Reflection;

namespace BDTest.Paths
{
    public static class FileLocations
    {
        public static string ReportsOutputDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(BDTestSettings.ReportFolderName))
                {
                    return Path.Combine(RawOutputDirectory, BDTestSettings.ReportFolderName);
                }
                
                return RawOutputDirectory;
            }
        } 
        public static string AggregatedJsonScenarios => Path.Combine(ReportsOutputDirectory, FileNames.TestDataJson);
        public static string Warnings => Path.Combine(ReportsOutputDirectory, FileNames.Warnings);
        

        public static string HtmlReportWithStoriesFilePath => Path.Combine(ReportsOutputDirectory, FileNames.ReportByStory);
        public static string HtmlReportWithoutStoriesFilePath => Path.Combine(ReportsOutputDirectory, FileNames.ReportAllScenarios);
        public static string ProjectDirectory => Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.Parent?.FullName;
        public static string RawOutputDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    public static class FileNames
    {
        public static readonly string TimeStamp = DateTime.Now.ToString("yyyyMMdd HH-mm-ss-fff");
        public static readonly string TestDataJson = $"BDTest - Test Data - {TimeStamp}.json";
        public static readonly string ReportByStory = $"BDTest - Report - By Story - {TimeStamp}.html";
        public static readonly string ReportAllScenarios = $"BDTest - Report - All Scenarios - {TimeStamp}.html";
        public static readonly string ReportFlakiness = $"BDTest - Report - Flakiness - {TimeStamp}.html";
        public static readonly string ReportTestTimesComparison = $"BDTest - Report - Test Times Comparison - {TimeStamp}.html";
        public static readonly string Warnings = "BDTest - Warnings.json";
    }
}
