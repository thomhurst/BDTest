using System;
using System.Collections.Generic;
using BDTest.Attributes;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace BDTest.Settings
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;

        public static DebugSettings Debug { get; } = new DebugSettings();
        
        public static List<object> CustomStringConverters { get; } = new List<object>();
        
        public static CustomExceptionSettings CustomExceptionSettings { get; } = new CustomExceptionSettings();

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
        public static SkipStepRules SkipStepRules { get; } = new SkipStepRules();
    }

    public class SkipStepRules
    {
        internal List<SkipStepRule> Rules = new List<SkipStepRule>();

        public void Add<T>(Func<bool> condition) where T : SkipStepAttribute
        {
            Rules.Add(new SkipStepRule(typeof(T), condition));
        }
    }
}
