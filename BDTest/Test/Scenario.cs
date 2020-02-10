using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
                Console.SetOut(TestOutputData.Instance);
            }
        }
        
        private readonly Reporter _reporters;

        [JsonProperty] public string Guid { get; private set; }
        [JsonProperty] public DateTime StartTime { get; private set; }

        [JsonProperty] public DateTime EndTime { get; private set; }

        [JsonProperty] public string FileName { get; private set; }
        
        [JsonProperty] public string TearDownOutput { get; set; }

        [JsonConstructor]
        private Scenario()
        {
        }

        [JsonIgnore] internal readonly TestDetails TestDetails;

        internal Scenario(List<Step> steps, TestDetails testDetails)
        {
            Guid = testDetails.GetGuid().ToString();
            FrameworkTestId = testDetails.TestId;
            
            TestMap.NotRun.TryRemove(testDetails.GetGuid(), out _);
            TestMap.StoppedEarly.TryAdd(testDetails.GetGuid(), this);

            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;

            TestDetails = testDetails;

            FileName = testDetails.CallerFile;

            Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            _reporters = new Reporters.Reporters();
            Steps = steps;
        }

        [JsonProperty]
        public string FrameworkTestId { get; set; }

        public async Task Execute()
        {
            try
            {
                await ExecuteInternal();
            }
            finally
            {
                TestMap.StoppedEarly.TryRemove(TestDetails.GetGuid(), out _);
                JsonLogger.WriteScenario(this);
            }
        }

        [JsonIgnore] public bool IsAsync { get; set; }

        [JsonProperty] public string Version { get; private set; }

        [JsonProperty] public List<Step> Steps { get; private set; }

        [JsonProperty] public string Output { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; private set; } = Status.Inconclusive;

        [JsonProperty] public StoryText StoryText { get; private set; }

        [JsonProperty] public ScenarioText ScenarioText { get; private set; }

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
        public TimeSpan TimeTaken { get; private set; }

        private async Task ExecuteInternal()
        {
            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;
                    _reporters.WriteStory(StoryText);
                    _reporters.WriteScenario(ScenarioText);
                    Steps.ForEach(step => _reporters.WriteLine(step.StepText));
                    _reporters.NewLine();

                    foreach (var step in Steps)
                    {
                        await step.Execute();
                    }

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
                    Output = string.Join(Environment.NewLine,
                        Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
                }
            });
        }
    }
}
