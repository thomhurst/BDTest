using System;

namespace BDTest.Settings
{
    public class ReportSettings
    {
        public string PersistentResultsDirectory { get; set; }
        public DateTime PersistentResultsCompareStartTime { get; set; } = DateTime.MinValue;
        public DateTime PrunePersistentDataOlderThan { get; set; } = DateTime.MinValue;
        public int PersistentFileCountToKeep { get; set; } = 365;

        public string ReportFolderName { get; set; } = "BDTestReports";
        public string ScenariosByStoryReportHtmlFilename { get; set; }
        public string AllScenariosReportHtmlFilename { get; set; }
        public string FlakinessReportHtmlFilename { get; set; }
        public string TestTimesReportHtmlFilename { get; set; }
        public string JsonDataFilename { get; set; }
    }
}