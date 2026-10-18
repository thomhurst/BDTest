using System.Diagnostics;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Extensions;
using BDTest.Test;

namespace BDTest.Helpers;

internal static class RetryAttributeHelper
{
    internal static BDTestRetryAttribute GetBDTestRetryAttribute(Scenario scenario) =>
        GetBDTestRetryAttribute(scenario, null);

    internal static BDTestRetryAttribute GetBDTestRetryAttribute(Scenario scenario, IEnumerable<StackFrame> stackFrames)
    {
        var scenarioMethod = scenario?.GetScenarioMethod();
        var bdTestRetryAttribute = scenarioMethod?.GetCustomAttribute<BDTestRetryAttribute>();
        if (bdTestRetryAttribute != null)
        {
            return bdTestRetryAttribute;
        }
            
        var methods = StackFramesHelper.GetMethods(stackFrames ?? StackFramesHelper.GetStackFrames()).ToArray();

        var attributeMethod = methods.FirstOrDefault(it => GetBDTestRetryAttribute(it) != null);
        if (attributeMethod != null)
        {
            return GetBDTestRetryAttribute(attributeMethod);
        }

        var storyClass = scenario?.GetStoryClass();
        bdTestRetryAttribute = storyClass?.GetCustomAttribute<BDTestRetryAttribute>();
        if (bdTestRetryAttribute != null)
        {
            return bdTestRetryAttribute;
        }

        var callingMethod = methods.FirstOrDefault(it => it.Name == scenario?.RuntimeInformation?.CallerMember);
        return callingMethod?.GetCustomAttribute<BDTestRetryAttribute>()
            ?? storyClass?.Assembly.GetCustomAttribute<BDTestRetryAttribute>();
    }
        
    private static BDTestRetryAttribute GetBDTestRetryAttribute(ICustomAttributeProvider it)
    {
        return it.GetCustomAttributes(typeof(BDTestRetryAttribute), true).FirstOrDefault() as BDTestRetryAttribute;
    }
}
