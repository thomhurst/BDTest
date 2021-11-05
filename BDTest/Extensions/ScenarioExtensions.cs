using System;
using System.Linq;
using System.Reflection;
using BDTest.Test;

namespace BDTest.Extensions
{
    internal static class ScenarioExtensions
    {
        internal static MethodInfo GetScenarioMethod(this Scenario scenario) => scenario?.BdTestBaseClass?.GetType()?.GetMethods()?.FirstOrDefault(x => x.Name == scenario?.RuntimeInformation?.CallerMember);
        internal static Type GetStoryClass(this Scenario scenario) => scenario?.BdTestBaseClass?.GetType();
    }
}