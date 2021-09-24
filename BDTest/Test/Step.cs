using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BDTest.Test
{
    public class Step
    {
        internal Runnable Runnable { get; }
        
        [JsonProperty]
        public string Guid { get; private set; }

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
        public ExceptionWrapper Exception { get; private set; }

        private bool _shouldSkip;

        internal Step(Runnable runnable, StepType stepType)
        {
            Guid = System.Guid.NewGuid().ToString("N");
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

        internal void ResetData()
        {
            Exception = null;
            Output = null;
            StartTime = DateTime.MinValue;
            EndTime = DateTime.MinValue;
            TimeTaken = TimeSpan.Zero;
            Status = Status.Inconclusive;
        }
        
        internal void SetStepText()
        {
            if (OverriddenStepText != null)
            {
                StepText = $"{StepPrefix} {OverriddenStepText.Invoke()}";
                return;
            }

            var customStepText = Runnable.Action != null ? StepTextHelper.GetStepText(Runnable.Action) : StepTextHelper.GetStepText(Runnable.Task);

            SetShouldSkip();
            
            StepText = $"{StepPrefix} {customStepText}";
        }

        private void SetShouldSkip()
        {
            var methodCallExpression = (Runnable.Action?.Body ?? Runnable.Task.Body) as MethodCallExpression;
            var methodInfo = methodCallExpression?.Method;

            var skipStepAttributes = (methodInfo?.GetCustomAttributes(
                                         typeof(SkipStepAttribute), true) ??
                                     new string[] { }) as SkipStepAttribute[];

            if (skipStepAttributes == null || !skipStepAttributes.Any())
            {
                return;
            }

            foreach (var skipStepAttributeOnStep in skipStepAttributes)
            {
                if (BDTestSettings.SkipStepRules.Rules.Any(skipStepRuleInSettings =>
                    skipStepRuleInSettings.AssociatedSkipAttributeType == skipStepAttributeOnStep.GetType()
                    && skipStepRuleInSettings.Condition(skipStepAttributeOnStep)))
                {
                    _shouldSkip = true;
                    break;   
                }
            }
        }

        internal async Task Execute()
        {
            SetStepText();

            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;

                    if (ShouldSkip())
                    {
                        StepText = $"[Skipped] {StepText}";
                        Status = Status.Skipped;
                        return;
                    }

                    await Runnable.Run();
                    Status = Status.Passed;
                }
                catch (NotImplementedException e)
                {
                    Status = Status.NotImplemented;
                    Exception = new ExceptionWrapper(e);
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
                    Status = Status.Failed;
                    Exception = new ExceptionWrapper(e);
                    throw;
                }
                finally
                {
                    EndTime = DateTime.Now;
                    TimeTaken = EndTime - StartTime;
                    Output = TestOutputData.Instance.ToString().Trim();
                    TestOutputData.ClearCurrentTaskData();
                }
            });
        }

        private bool ShouldSkip()
        {
            if (StepType == StepType.When && BDTestSettings.Debug.ShouldSkipWhenStep)
            {
                return true;
            }

            return _shouldSkip;
        }
    }
}