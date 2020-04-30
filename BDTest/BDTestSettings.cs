using System;
using System.Collections.Concurrent;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace BDTest
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;

        public static DebugSettings Debug { get; } = new DebugSettings();
        
        public static ConcurrentBag<Type> SuccessExceptionTypes { get; } = new ConcurrentBag<Type>();

        public static string PersistentResultsDirectory { get; set; }
        public static DateTime PersistentResultsCompareStartTime { get; set; } = DateTime.MinValue;
        public static DateTime PrunePersistentDataOlderThan { get; set; } = DateTime.MinValue;
        public static int PersistentFileCountToKeep { get; set; } = 365;

        public static string ReportFolderName { get; set; } = "BDTestReports";
        public static string ScenariosByStoryReportHtmlFilename { get; set; }
        public static string AllScenariosReportHtmlFilename { get; set; }
        public static string FlakinessReportHtmlFilename { get; set; }
        public static string TestTimesReportHtmlFilename { get; set; }
        public static string JsonDataFilename { get; set; }
    }

    public class DebugSettings
    {
        public bool ShouldSkipWhenStep { get; set; }
    }
}
