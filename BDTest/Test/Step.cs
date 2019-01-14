using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using BDTest.Attributes;
using BDTest.Output;
using BDTest.Test.Steps;

namespace BDTest.Test
{
    public class Step
    {
        internal Expression<Action> Action { get; }

        [JsonProperty]
        public  DateTime StartTime;

        [JsonProperty]
        public DateTime EndTime;

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        private readonly StepType _stepType;
        private string StepPrefix => _stepType.GetValue();

        [JsonProperty]
        public string Output { get; private set; }

        [JsonConverter(typeof(TimespanConverter))]
        [JsonProperty]
        public TimeSpan TimeTaken;

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; set; } = Status.Inconclusive;

        [JsonProperty]
        public Exception Exception;

        internal Step(Expression<Action> action, StepType stepType)
        {
            Action = action;
            _stepType = stepType;
            SetStepText();
        }

        [JsonConstructor]
        private Step()
        {

        }

        [JsonProperty]
        public string StepText { get; private set; }

        public void SetStepText()
        {
            var customStepText =
                ((StepTextAttribute)(((Action.Body as MethodCallExpression)?.Method.GetCustomAttributes(
                                          typeof(StepTextAttribute), true) ??
                                      new string[] { }).FirstOrDefault()))?.Text;
            var methodNameHumanized = (Action.Body as MethodCallExpression)?.Method.Name.Humanize();
            StepText = $"{StepPrefix} {customStepText ?? methodNameHumanized}";
        }

        public void Execute()
        {
            var task = new Task(() =>
            {
                try
                {
                    StartTime = DateTime.Now;
                    Action.Compile().Invoke();
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
                    Status = Status.Failed;
                    Exception = e;
                    throw;
                }
                finally
                {
                    EndTime = DateTime.Now;
                    TimeTaken = EndTime - StartTime;
                    Output = ConsoleTextInterceptor.Instance.ToString();
                    ConsoleTextInterceptor.Instance.ClearCurrentTaskData();
                }
            });
            task.Start();
            task.Wait();
        }

        public void SetEndTime() {
            EndTime = DateTime.Now;
            TimeTaken = EndTime - StartTime;
        }
    }
}