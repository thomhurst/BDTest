using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Test;
using Humanizer;

namespace BDTest.Helpers
{
    internal static class ScenarioTextHelper
    {
        internal  static ScenarioText GetScenarioText(string callerMember, out IEnumerable<string> parameters)
        {
            var stackFrames = new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();

            var stepAttributeFrame = stackFrames.FirstOrDefault(it => GetScenarioTextAttribute(it) != null);
            if (stepAttributeFrame != null)
            {
                parameters = GetParameters(stepAttributeFrame);
                return new ScenarioText($"{GetScenarioTextAttribute(stepAttributeFrame)}");
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == callerMember);
            if (callingFrame != null)
            {
                parameters = GetParameters(callingFrame);
                return new ScenarioText($"{callingFrame.GetMethod().Name.Humanize()}");
            }

            parameters = Array.Empty<string>();
            return new ScenarioText("No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests");
        }
        
        private static string GetScenarioTextAttribute(StackFrame it)
        {
            return GetScenarioTextAttribute(it.GetMethod());
        }
        
        private static string GetScenarioTextAttribute(ICustomAttributeProvider it)
        {
            return (it.GetCustomAttributes(typeof(ScenarioTextAttribute), true).FirstOrDefault() as ScenarioTextAttribute)?.Text;
        }
        
        private static IEnumerable<string> GetParameters(StackFrame callingFrame)
        {
            return callingFrame?.GetMethod()?.GetParameters().Select(it => it.Name);
        }
    }
}