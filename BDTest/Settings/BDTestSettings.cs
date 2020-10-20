using System.Collections.Generic;
using BDTest.Settings.Retry;
using BDTest.Settings.Skip;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace BDTest.Settings
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;
        
        public static string Environment { get; set; }
        
        public static string Tag { get; set; }
        
        public static string BranchName { get; set; }
        
        public static Dictionary<string, string> CustomProperties { get; } = new Dictionary<string, string>();
        
        public static DebugSettings Debug { get; } = new DebugSettings();

        public static List<object> CustomStringConverters { get; } = new List<object>();

        public static CustomExceptionSettings CustomExceptionSettings { get; } = new CustomExceptionSettings();

        public static ReportSettings ReportSettings { get; } = new ReportSettings();

        public static SkipStepRules SkipStepRules { get; } = new SkipStepRules();
        public static RetryTestRules RetryTestRules { get; } = new RetryTestRules();
    }
}
