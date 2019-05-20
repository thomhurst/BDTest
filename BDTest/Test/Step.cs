using System;
using System.Collections.Generic;
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
        internal Runnable Runnable { get; }

        [JsonProperty]
        public DateTime StartTime { get; private set; }

        [JsonProperty]
        public DateTime EndTime { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
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

        private void SetStepText()
        {
            MethodCallExpression methodCallExpression;
            if (Runnable.Action != null)
            {
                methodCallExpression = Runnable.Action.Body as MethodCallExpression;
            }
            else
            {
                methodCallExpression = Runnable.Task.Body as MethodCallExpression;
            }

            var arguments = new List<string>();

            if (methodCallExpression?.Arguments != null)
            {
                foreach (var argument in methodCallExpression.Arguments)
                {
                    var value = Expression.Lambda(argument).Compile().DynamicInvoke();
                    arguments.Add(value.ToString());
                }
            }

            var methodInfo = methodCallExpression?.Method;
            var customStepText =
                ((StepTextAttribute)(methodInfo?.GetCustomAttributes(
                                         typeof(StepTextAttribute), true) ??
                                     new string[] { }).FirstOrDefault())?.Text;
            var methodNameHumanized = methodInfo?.Name.Humanize();

            if (customStepText != null && arguments != null)
            {
                try
                {
                    customStepText = string.Format(customStepText, arguments.ToArray());
                }
                catch (Exception)
                {
                    throw new ArgumentException(
                        $"Step Text arguments are wrong.\nTemplate is: {customStepText}\nArguments are {string.Join(",", arguments)}");
                }
            }

            StepText = $"{StepPrefix} {customStepText ?? methodNameHumanized}";
        }

        public async Task Execute()
        {
            await Task.Run(async () =>
            {
                try
                {
                    StartTime = DateTime.Now;
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
                    Status = Status.Failed;
                    Exception = e;
                    throw;
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
    }
}