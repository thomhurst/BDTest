using System;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BDTest.Test
{
    public class Step
    {
        internal Runnable Runnable { get; }

        [JsonProperty]
        public DateTime StartTime { get; private set; }

        [JsonProperty]
        public DateTime EndTime { get; private set; }
        
        [JsonProperty]
        private StepType StepType { get; }
        private string StepPrefix => StepType.GetValue();

        [JsonProperty]
        public string Output { get; private set; }

        [JsonConverter(typeof(TimespanConverter))]
        [JsonProperty]
        public TimeSpan TimeTaken { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; private set; } = Status.Inconclusive;

        [JsonProperty]
        public Exception Exception { get; private set; }

        private bool _alreadyExecuted;

        internal Step(Runnable runnable, StepType stepType)
        {
            Runnable = runnable;
            StepType = stepType;
            SetStepText();
        }

        [JsonConstructor]
        private Step()
        {

        }

        [JsonProperty]
        public string StepText { get; private set; }
        
        [JsonIgnore]
        internal Func<string> OverriddenStepText { get; set; }

        internal void SetStepText()
        {
            if (OverriddenStepText != null)
            {
                StepText = $"{StepPrefix} {OverriddenStepText.Invoke()}";
                return;
            }

            var customStepText = Runnable.Action != null ? StepTextHelper.GetStepText(Runnable.Action) : StepTextHelper.GetStepText(Runnable.Task);

            StepText = $"{StepPrefix} {customStepText}";
        }

        internal async Task Execute()
        {
            SetStepText();

            CheckIfAlreadyExecuted();

            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;

                    if (StepType == StepType.When && BDTestSettings.Debug.ShouldSkipWhenStep)
                    {
                        StepText = $"[Skipped due to Debug Settings] {StepText}";
                        Status = Status.SkippedDueToDebugSettings;
                        return;
                    }
                    
                    await Runnable.Run();
                    Status = Status.Passed;
                }
                catch (NotImplementedException e)
                {
                    Status = Status.NotImplemented;
                    Exception = e;
                    throw;
                }
                catch (Exception e)
                {
                    if (BDTestSettings.SuccessExceptionTypes.Contains(e.GetType()))
                    {
                        Status = Status.Passed;
                    }
                    else
                    {
                        Status = Status.Failed;
                        Exception = e;
                        throw;
                    }
                }
                finally
                {
                    EndTime = DateTime.Now;
                    TimeTaken = EndTime - StartTime;
                    Output = TestOutputData.Instance.ToString();
                    TestOutputData.ClearCurrentTaskData();
                }
            });
        }

        private void CheckIfAlreadyExecuted()
        {
            if (_alreadyExecuted)
            {
                throw new AlreadyExecutedException("This step has already been executed");
            }

            _alreadyExecuted = true;
        }
    }
}