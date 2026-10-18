using BDTest.Metadata;

namespace BDTest.Helpers;

internal static class ScenarioTextHelper
{
    internal static string GetScenarioText(TestMethodMetadata? metadata, string? callerMember) =>
        metadata?.ScenarioText ?? (callerMember != null && !string.IsNullOrEmpty(callerMember)
            ? NameHumanizer.Humanize(callerMember)
            : "No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests");
}
