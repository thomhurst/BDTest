using System;
using System.Collections.Generic;
using BDTest.Attributes;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace BDTest.Settings
{
    public static class BDTestSettings
    {
        public static bool InterceptConsoleOutput { get; set; } = true;
        public static string Environment { get; set; }
        public static string Tag { get; set; }

        public static DebugSettings Debug { get; } = new DebugSettings();
        
        public static List<object> CustomStringConverters { get; } = new List<object>();
        
        public static CustomExceptionSettings CustomExceptionSettings { get; } = new CustomExceptionSettings();
        
        public static ReportSettings ReportSettings { get; } = new ReportSettings();
        
        public static SkipStepRules SkipStepRules { get; } = new SkipStepRules();
    }

    public class SkipStepRules
    {
        internal List<SkipStepRule<object>> Rules = new List<SkipStepRule<object>>();

        public void Add<T>(Func<T, bool> condition) where T : SkipStepAttribute
        {
            // Cast to Func<object, bool>
            Func<object, bool> func = value => condition((T) value);
            Rules.Add(new SkipStepRule<object>(typeof(T), func));
        }
    }
}
