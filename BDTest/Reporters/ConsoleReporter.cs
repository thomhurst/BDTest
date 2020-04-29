using System;
using BDTest.Test;

namespace BDTest.Reporters
{
    internal class ConsoleReporter : Reporter
    {

        public override void WriteLine(string text, params object[] args)
        {
            Console.Out.WriteLine(text, args);
        }

        public override void WriteStory(StoryText storyText)
        {
            if (storyText?.Story == null)
            {
                return;
            }

            WriteLine("Story: " + storyText.Story);
        }

        public override void WriteScenario(ScenarioText scenarioText)
        {
            if (scenarioText?.Scenario == null)
            {
                return;
            }

            WriteLine("Scenario: " + scenarioText.Scenario);
        }

        public override void OnFinish()
        {
            // Nothing Needed to Finish with the console.
        }
    }
}
