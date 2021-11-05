using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Extensions;
using BDTest.Test;
using Humanizer;

namespace BDTest.Helpers
{
    internal static class ScenarioTextHelper
    {
        internal static ScenarioTextWithParameters GetScenarioTextWithParameters(MethodInfo testMethod, string callerMember)
        {
            var stackFrames = new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();

            var stepAttributeFrame = stackFrames.FirstOrDefault(it => GetScenarioTextAttribute(it) != null);
            if (stepAttributeFrame != null)
            {
                return new ScenarioTextWithParameters(new ScenarioText(GetScenarioTextAttribute(stepAttributeFrame)), GetParameters(stepAttributeFrame));
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == callerMember);
            if (callingFrame != null)
            {
                return new ScenarioTextWithParameters(new ScenarioText(callingFrame.GetMethod().Name.Humanize()), GetParameters(callingFrame));
            }
            
            var scenarioTextAttribute = testMethod?.GetCustomAttribute<ScenarioTextAttribute>();
            if (scenarioTextAttribute != null)
            {
                return new ScenarioTextWithParameters(new ScenarioText(scenarioTextAttribute.Text),
                    Enumerable.Empty<string>());
            }
            
            return new ScenarioTextWithParameters(new ScenarioText("No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests"), Array.Empty<string>());
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

    internal class ScenarioTextWithParameters
    {
        public ScenarioText ScenarioText { get; }
        public IEnumerable<string> Parameters { get; }

        internal ScenarioTextWithParameters(ScenarioText scenarioText, IEnumerable<string> parameters)
        {
            ScenarioText = scenarioText;
            Parameters = parameters;
        }
    }
}