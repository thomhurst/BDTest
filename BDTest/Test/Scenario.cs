using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Exceptions;
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

        [JsonProperty] internal string Guid { get; private set; }
        [JsonProperty] public DateTime StartTime { get; private set; }

        [JsonProperty] public DateTime EndTime { get; private set; }

        [JsonProperty] public string FileName { get; private set; }
        
        [JsonProperty]
        public string TestStartupInformation { get; set; }
        
        [JsonProperty] public string TearDownOutput { get; set; }

        [JsonConstructor]
        private Scenario()
        {
        }

        [JsonIgnore] internal readonly TestDetails TestDetails;
        
        private bool _alreadyExecuted;

        internal Scenario(List<Step> steps, TestDetails testDetails)
        {
            Guid = testDetails.GetGuid().ToString();
            FrameworkTestId = testDetails.TestId;
            
            TestHolder.NotRun.TryRemove(testDetails.GetGuid(), out _);
            TestHolder.StoppedEarly.TryAdd(testDetails.GetGuid(), this);
            TestHolder.Scenarios.Add(this);

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

        internal async Task Execute()
        {
            try
            {
                await ExecuteInternal();
            }
            finally
            {
                TestHolder.StoppedEarly.TryRemove(TestDetails.GetGuid(), out _);
            }
        }

        [JsonProperty] public string Version { get; private set; }

        [JsonProperty] public List<Step> Steps { get; private set; }

        [JsonProperty] public string Output { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; private set; } = Status.Inconclusive;

        [JsonProperty] internal StoryText StoryText { get; private set; }

        [JsonProperty] internal ScenarioText ScenarioText { get; private set; }

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
        
        private void CheckIfAlreadyExecuted()
        {
            if (_alreadyExecuted)
            {
                throw new AlreadyExecutedException("This scenario has already been executed");
            }

            _alreadyExecuted = true;
        }

        private async Task ExecuteInternal()
        {
            CheckIfAlreadyExecuted();
            
            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;
                    
                    WriteStoryAndScenario();

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
                    
                    _reporters.WriteLine($"{Environment.NewLine}Exception: {e.StackTrace}{Environment.NewLine}");

                    throw;
                }
                finally
                {
                    foreach (var notRunStep in Steps.Where(step => step.Status == Status.Inconclusive))
                    {
                        notRunStep.SetStepText();
                    }
                    
                    _reporters.WriteLine($"{Environment.NewLine}Test Summary:{Environment.NewLine}");
                    
                    Steps.ForEach(step => _reporters.WriteLine($"{step.StepText} > [{step.Status}]"));
                    
                    _reporters.WriteLine($"{Environment.NewLine}Test Result: {Status}{Environment.NewLine}");
                    
                    EndTime = DateTime.Now;
                    TimeTaken = EndTime - StartTime;
                    
                    Output = string.Join(Environment.NewLine,
                        Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
                }
            });
        }

        private void WriteStoryAndScenario()
        {
            _reporters.WriteStory(StoryText);
            _reporters.WriteScenario(ScenarioText);
            _reporters.NewLine();
            
            TestStartupInformation = TestOutputData.Instance.ToString();
            TestOutputData.ClearCurrentTaskData();
        }
    }
}
