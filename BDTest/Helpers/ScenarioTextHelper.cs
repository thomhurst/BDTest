using System.Diagnostics;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Test;
using Humanizer;

namespace BDTest.Helpers;

internal static class ScenarioTextHelper
{
    internal static ScenarioTextWithParameters GetScenarioTextWithParameters(MethodInfo testMethod, string callerMember) =>
        GetScenarioTextWithParameters(testMethod, callerMember, StackFramesHelper.GetStackFrames());

    internal static ScenarioTextWithParameters GetScenarioTextWithParameters(MethodInfo testMethod, string callerMember,
        IEnumerable<StackFrame> stackFrames)
    {
        var methods = StackFramesHelper.GetMethods(stackFrames).ToArray();
        var attributeMethod = methods.FirstOrDefault(it => GetScenarioTextAttribute(it) != null);
        if (attributeMethod != null)
        {
            return new ScenarioTextWithParameters(new ScenarioText(GetScenarioTextAttribute(attributeMethod)), GetParameters(attributeMethod));
        }

        var callingMethod = methods.FirstOrDefault(it => it.Name == callerMember);
        if (callingMethod != null)
        {
            return new ScenarioTextWithParameters(new ScenarioText(callingMethod.Name.Humanize()), GetParameters(callingMethod));
        }
            
        var scenarioTextAttribute = testMethod?.GetCustomAttribute<ScenarioTextAttribute>();
        if (scenarioTextAttribute != null)
        {
            return new ScenarioTextWithParameters(new ScenarioText(scenarioTextAttribute.Text),
                Enumerable.Empty<string>());
        }
            
        if (!string.IsNullOrEmpty(callerMember))
        {
            return new ScenarioTextWithParameters(new ScenarioText(callerMember.Humanize()), Array.Empty<string>());
        }

        return new ScenarioTextWithParameters(new ScenarioText("No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests"), Array.Empty<string>());
    }
        
    private static string GetScenarioTextAttribute(ICustomAttributeProvider it)
    {
        return (it.GetCustomAttributes(typeof(ScenarioTextAttribute), true).FirstOrDefault() as ScenarioTextAttribute)?.Text;
    }
        
    private static IEnumerable<string> GetParameters(MethodBase method)
    {
        return method.GetParameters().Select(it => it.Name);
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