using System;

namespace BDTest
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;

        public static string PersistentResultsDirectory { get; set; }
        public static DateTime PersistentResultsCompareStartTime { get; set; } = DateTime.MinValue;

        public static string ScenariosByStoryReportHtmlFilename { get; set; }
        public static string AllScenariosReportHtmlFilename { get; set; }
        public static string FlakinessReportHtmlFilename { get; set; }
        public static string TestTimesReportHtmlFilename { get; set; }
        public static string JsonDataFilename { get; set; }
        public static string XmlDataFilename { get; set; }

    }
}
