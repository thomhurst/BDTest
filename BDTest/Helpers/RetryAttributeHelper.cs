using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Extensions;
using BDTest.Test;

namespace BDTest.Helpers
{
    internal static class RetryAttributeHelper
    {
        internal static BDTestRetryAttribute GetBDTestRetryAttribute(Scenario scenario)
        {
            var scenarioMethod = scenario?.GetScenarioMethod();
            var bdTestRetryAttribute = scenarioMethod?.GetCustomAttribute<BDTestRetryAttribute>();
            if (bdTestRetryAttribute != null)
            {
                return bdTestRetryAttribute;
            }
            
            var stackFrames = new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();

            var stepAttributeFrame = stackFrames.FirstOrDefault(it => GetBDTestRetryAttribute(it) != null);
            if (stepAttributeFrame != null)
            {
                return GetBDTestRetryAttribute(stepAttributeFrame);
            }

            var storyClass = scenario?.GetStoryClass();
            bdTestRetryAttribute = storyClass?.GetCustomAttribute<BDTestRetryAttribute>();
            if (bdTestRetryAttribute != null)
            {
                return bdTestRetryAttribute;
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == scenario?.RuntimeInformation?.CallerMember);
            return callingFrame?.GetMethod()?.GetCustomAttribute<BDTestRetryAttribute>();
        }
        
        private static BDTestRetryAttribute GetBDTestRetryAttribute(StackFrame it)
        {
            return GetBDTestRetryAttribute(it.GetMethod());
        }
        
        private static BDTestRetryAttribute GetBDTestRetryAttribute(ICustomAttributeProvider it)
        {
            return it.GetCustomAttributes(typeof(BDTestRetryAttribute), true).FirstOrDefault() as BDTestRetryAttribute;
        }
    }
}