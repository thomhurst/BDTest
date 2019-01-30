using System;

namespace BDTest
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;

        public static string PersistentResultsDirectory { get; set; } = null;
        public static DateTime PersistentResultsCompareStartTime { get; set; } = DateTime.MinValue;

        public static string ScenariosByStoryReportHtmlFilename { get; set; } = null;
        public static string AllScenariosReportHtmlFilename { get; set; } = null;
        public static string FlakinessReportHtmlFilename { get; set; } = null;
        public static string TestTimesReportHtmlFilename { get; set; } = null;
        public static string JsonDataFilename { get; set; } = null;
        public static string XmlDataFilename { get; set; } = null;

    }
}
