using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using BDTest.Attributes;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Reporters;

namespace BDTest.Test
{
    public class Scenario
    {

        static Scenario()
        {
            if (BDTestSettings.InterceptConsoleOutput)
            {
                Console.SetOut(ConsoleTextInterceptor.Instance);
            }
        }

        private readonly Reporter _reporters;

        [JsonProperty]
        public DateTime StartTime;

        [JsonProperty]
        public DateTime EndTime;

        [JsonConstructor]
        private Scenario() { }

        internal Scenario(List<Step> steps, TestDetails testDetails)
        {
            TestMap.Testables.TryRemove(testDetails.GetGuid(), out _);

            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;

            _reporters = new Reporters.Reporters();
            Steps = steps;
            
            try
            {
                Execute();
            }
            finally
            {
                JsonLogger.WriteScenario(this);
            }
        }

        [JsonIgnore]
        public bool IsAsync { get; set; }

        [JsonProperty]
        public List<Step> Steps { get; set; }

        [JsonProperty]
        public string Output { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; set; } = Status.Inconclusive;

        [JsonProperty] public StoryText StoryText;

        [JsonProperty] public ScenarioText ScenarioText;

        public string GetScenarioText()
        {
            return ScenarioText?.Scenario ?? "Scenario Title Not Defined";
        }

        public string GetStoryText()
        {
            return StoryText?.Story ?? "Story Text Not Defined";
        }

        [JsonConverter(typeof(TimespanConverter))]
        [JsonProperty]
        public TimeSpan TimeTaken;

        internal void Execute()
        {
            var task = new Task( () => 
            {
                try
                {
                    StartTime = DateTime.Now;
                    _reporters.WriteStory(StoryText);
                    _reporters.WriteScenario(ScenarioText);
                    Steps.ForEach(step => _reporters.WriteLine(step.StepText));
                    _reporters.NewLine();
                    Steps.ForEach(step => step.Execute());
                    Status = Status.Passed;
                }
                catch (NotImplementedException)
                {
                    Status = Status.NotImplemented;
                }
                catch (Exception e)
                {
                    Status = Status.Failed;
                    _reporters.WriteLine($"Exception: {e.StackTrace}");
                    throw;
                }
                finally
                {
                    _reporters.WriteLine($"{Environment.NewLine}Test Result: {Status}");
                    EndTime = DateTime.Now;
                    TimeTaken = EndTime - StartTime;
                    Output = string.Join(Environment.NewLine, Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
                }
            });

            task.Start();
            task.Wait();
        }
    }
}
