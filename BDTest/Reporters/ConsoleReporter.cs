using BDTest.Output;
using BDTest.Test;

namespace BDTest.Reporters;

internal static class ConsoleReporter
{
    public static async Task WriteLine(string? text)
    {
        await Console.Out.WriteLineAsync(text);
    }

    public static void WriteLineToConsoleOnly(string? text)
    {
        TestOutputData.Instance.WriteLineToConsoleOnly(text);
    }

    public static async Task Write(string? text)
    {
        await Console.Out.WriteAsync(text);
    }

    public static async Task WriteStory(StoryText? storyText)
    {
        if (storyText?.Story == null)
        {
            return;
        }

        await Write("Story: ");
        await WriteLine(storyText.Story);
    }

    public static async Task WriteScenario(ScenarioText? scenarioText)
    {
        if (scenarioText?.Scenario == null)
        {
            return;
        }

        await Write("Scenario: ");
        await WriteLine(scenarioText.Scenario?.Trim());
    }
}