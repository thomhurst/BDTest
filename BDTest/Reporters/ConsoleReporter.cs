using System;
using BDTest.Test;

namespace BDTest.Reporters
{
    internal static class ConsoleReporter
    {
        public static void WriteLine(string text)
        {
            Console.Out.WriteLine(text);
        }

        public static void WriteStory(StoryText storyText)
        {
            if (storyText?.Story == null)
            {
                return;
            }

            WriteLine("Story: " + storyText.Story);
        }

        public static void WriteScenario(ScenarioText scenarioText)
        {
            if (scenarioText?.Scenario == null)
            {
                return;
            }

            WriteLine("Scenario: " + scenarioText.Scenario);
        }
    }
}
