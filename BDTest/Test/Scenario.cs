using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Reporters;
using BDTest.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        
        [JsonIgnore]
        private readonly Reporter _reporters;
        
        [JsonIgnore]
        private readonly TestDetails _testDetails;

        [JsonProperty] public string Guid { get; private set; }
        [JsonProperty] public DateTime StartTime { get; private set; }

        [JsonProperty] public DateTime EndTime { get; private set; }

        [JsonProperty] public string FileName { get; private set; }
        
        [JsonProperty]
        public string TestStartupInformation { get; set; }

        [JsonIgnore] public ExceptionWrapper Exception => Steps.FirstOrDefault(step => step.Exception != null)?.Exception;
        
        [JsonProperty] public string TearDownOutput { get; set; }
        
        [JsonProperty] public string CustomHtmlOutputForReport { get; set; }

        [JsonConstructor]
        private Scenario()
        {
        }

        private bool _alreadyExecuted;

        internal Scenario(List<Step> steps, TestDetails testDetails)
        {
            _testDetails = testDetails;
            Guid = testDetails.GetGuid();
            FrameworkTestId = testDetails.TestId;
            
            TestHolder.NotRun.TryRemove(Guid, out _);
            TestHolder.AddScenario(this);

            StoryText = testDetails.StoryText;
            ScenarioText = testDetails.ScenarioText;
            CustomTestInformation = testDetails.CustomTestInformation;

            FileName = testDetails.CallerFile;

            _reporters = new Reporters.Reporters();
            Steps = steps;
        }

        [JsonProperty]
        public string FrameworkTestId { get; set; }

        internal async Task Execute()
        {
            await ExecuteInternal().ConfigureAwait(false);
        }

        [JsonProperty] public List<Step> Steps { get; private set; }

        [JsonProperty] public string Output { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; private set; } = Status.Inconclusive;

        [JsonProperty] internal StoryText StoryText { get; private set; }

        [JsonProperty] internal ScenarioText ScenarioText { get; private set; }
        
        [JsonProperty] public TestInformationAttribute[] CustomTestInformation { get; private set; }
        
        [JsonIgnore] internal bool ShouldRetry { get; private set; }
        [JsonProperty] public int RetryCount { get; private set; }

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
        
        private async Task CheckIfAlreadyExecuted()
        {
            if (ShouldRetry)
            {
                await SetRetryValues().ConfigureAwait(false);
                return;
            }
            
            if (_alreadyExecuted)
            {
                throw new AlreadyExecutedException("This scenario has already been executed");
            }

            _alreadyExecuted = true;
        }

        private async Task SetRetryValues()
        {
            ShouldRetry = false;
            
            RetryCount++;
            
            foreach (var step in Steps)
            {
                step.ResetData();
            }

            Status = Status.Inconclusive;

            try
            {
                await _testDetails.BdTestBase.RunMethodWithAttribute<BDTestRetryTearDownAttribute>();
                await _testDetails.BdTestBase.RunMethodWithAttribute<BDTestRetrySetUpAttribute>();
            
                await _testDetails.BdTestBase.OnRetry();
            }
            catch (Exception e)
            {
                throw new ErrorOccurredDuringRetryActionException(e);
            }

            _reporters.WriteLine("\nRetrying test...\n");
        }

        private async Task ExecuteInternal()
        {
            await CheckIfAlreadyExecuted().ConfigureAwait(false);
            
            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;
                    
                    TestOutputData.ClearCurrentTaskData();

                    if (RetryCount == 0)
                    {
                        WriteTestInformation();
                    }

                    foreach (var step in Steps)
                    {
                        await step.Execute();
                    }

                    Status = Status.Passed;
                }
                catch (NotImplementedException)
                {
                    Status = Status.NotImplemented;
                    throw;
                }
                catch (Exception e) when (BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(e.GetType()))
                {
                    Status = Status.Passed;
                    throw;
                }
                catch (Exception e) when (BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(e.GetType()))
                {
                    Status = Status.Inconclusive;
                    throw;
                }
                catch (Exception e)
                {
                    var validRetryRules = BDTestSettings.RetryTestRules.Rules.Where(rule => rule.Condition(e)).ToList();
                    if (validRetryRules.Any() && RetryCount < validRetryRules.Max(x => x.RetryLimit))
                    {
                        ShouldRetry = true;
                        return;
                    }
                    
                    Status = Status.Failed;
                    
                    _reporters.WriteLine($"{Environment.NewLine}Exception: {e.StackTrace}{Environment.NewLine}");

                    throw;
                }
                finally
                {
                    if (ShouldRetry)
                    {
                        await ExecuteInternal();
                    }
                    else
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
                }
            });
        }

        private void WriteTestInformation()
        {
            WriteStoryAndScenario();
            
            WriteCustomTestInformation();
            
            TestOutputData.ClearCurrentTaskData();
        }

        private void WriteCustomTestInformation()
        {
            foreach (var testInformationAttribute in CustomTestInformation ?? Array.Empty<TestInformationAttribute>())
            {
                _reporters.WriteLine(testInformationAttribute.Print());
            }
            
            _reporters.NewLine();
        }

        private void WriteStoryAndScenario()
        {
            _reporters.WriteStory(StoryText);
            
            _reporters.WriteScenario(ScenarioText);
            
            _reporters.NewLine();
        }
    }
}
