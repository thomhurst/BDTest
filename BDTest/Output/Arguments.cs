namespace BDTest.Output
{
    public static class Arguments
    {
        public static string ResultDirectoryArgumentName { get; } = "-ResultsDirectory=";
        public static string PersistentStorageArgumentName { get; } = "-PersistentStorageDirectory=";
        public static string PersistentResultsCompareStartTimeArgumentName { get; } = "-PersistentResultsStartCompareTime=";
        public static string ScenariosByStoryReportHtmlFilenameArgumentName { get; } = "-ScenariosByStoryReportHtmlFilename=";
        public static string AllScenariosReportHtmlFilenameArgumentName { get; } = "-AllScenariosReportHtmlFilename=";
        public static string FlakinessReportHtmlFilenameArgumentName { get; } = "-FlakinessReportHtmlFilename=";
        public static string TestTimesReportHtmlFilenameArgumentName { get; } = "-TestTimesReportHtmlFilename=";
        public static string JsonDataFilenameArgumentName { get; } = "-JsonDataFilename=";
        public static string XmlDataFilenameArgumentName { get; } = "-XmlDataFilename=";
    }
}