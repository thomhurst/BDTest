using BDTest.Test;

namespace BDTest.Reporters;

internal static class ConsoleReporter
{
    public static async Task WriteLine(string text, ConsoleColor? consoleColor = null)
    {
        if (consoleColor != null)
        {
            Console.ForegroundColor = consoleColor.Value;
        }
            
        await Console.Out.WriteLineAsync(text);
        Console.ResetColor();
    }
        
    public static async Task Write(string text, ConsoleColor? consoleColor = null)
    {
        if (consoleColor != null)
        {
            Console.ForegroundColor = consoleColor.Value;
        }

        await Console.Out.WriteAsync(text);
        Console.ResetColor();
    }

    public static async Task WriteStory(StoryText storyText)
    {
        if (storyText?.Story == null)
        {
            return;
        }

        await Write("Story: ", ConsoleColor.DarkRed);
        await WriteLine(storyText.Story);
    }

    public static async Task WriteScenario(ScenarioText scenarioText)
    {
        if (scenarioText?.Scenario == null)
        {
            return;
        }

        await Write("Scenario: ", ConsoleColor.DarkRed);
        await WriteLine(scenarioText.Scenario?.Trim());
    }
}