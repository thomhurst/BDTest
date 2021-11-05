using System;
using System.Reflection;
using BDTest.Test;

namespace BDTest.Extensions
{
    internal static class ScenarioExtensions
    {
        internal static MethodInfo GetScenarioMethod(this Scenario scenario) => scenario?.BdTestBaseClass?.GetType()?.GetMethod(scenario.RuntimeInformation.CallerMember);
        internal static Type GetStoryClass(this Scenario scenario) => scenario?.BdTestBaseClass?.GetType();
    }
}