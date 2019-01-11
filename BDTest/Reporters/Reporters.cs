using System;
using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.Reporters
{
    public class Reporters : Reporter
    {
        private static readonly List<Reporter> CustomReporters = new List<Reporter>();
        private readonly List<Reporter> _reporters;
        public static void AddReporter<T>(T t) where T : Reporter, new()
        {
            CustomReporters.Add(t);
        }

        public Reporters()
        {
            _reporters = new List<Reporter>
            {
                new ConsoleReporter(),
                new FileReporter()
            };

            foreach (var customReporter in CustomReporters)
            {
                _reporters.Add(Activator.CreateInstance(customReporter.GetType()) as Reporter);
            }

            WriteStartLine();
        }

        public override void WriteLine(string text, params object[] args)
        {
            foreach (var reporter in _reporters)
            {
                reporter.WriteLine(text, args);
            }
        }

        public override void WriteStory(StoryText storyText)
        {
            if (storyText?.Story == null) return;
            foreach (var reporter in _reporters)
            {
                reporter.WriteStory(storyText);
            }
        }

        public override void WriteScenario(ScenarioText scenarioText)
        {
            if (scenarioText?.Scenario == null) return;
            foreach (var reporter in _reporters)
            {
                reporter.WriteScenario(scenarioText);
            }
        }

        public override void OnFinish()
        {
            foreach (var reporter in _reporters)
            {
                reporter.OnFinish();
            }
        }
    }
}
