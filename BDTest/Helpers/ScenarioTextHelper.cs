using BDTest.Metadata;
using Humanizer;

namespace BDTest.Helpers;

internal static class ScenarioTextHelper
{
    internal static string GetScenarioText(TestMethodMetadata metadata, string callerMember) =>
        metadata?.ScenarioText ?? (!string.IsNullOrEmpty(callerMember)
            ? callerMember.Humanize()
            : "No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests");
}
