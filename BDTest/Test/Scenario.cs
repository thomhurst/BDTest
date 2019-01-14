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

        internal Scenario(List<Step> steps, string callerMember)
        {
            _callerMember = callerMember;
            _reporters = new Reporters.Reporters();
            Steps = steps;
            SetStoryText();
            SetScenarioText();
            try
            {
                Execute();
            }
            finally
            {
                JsonLogger.WriteScenario(this);
            }
        }

        [JsonProperty]
        internal StoryText StoryText { get; private set; }

        [JsonProperty]
        internal ScenarioText ScenarioText { get; private set; }

        public string GetScenarioText()
        {
            return ScenarioText?.Scenario ?? "Scenario Title Not Defined";
        }

        public string GetStoryText()
        {
            return StoryText?.Story ?? "Story Text Not Defined";
        }

        private void SetStoryText()
        {
            var classStoryAttribute =
                FindStoryAttribute();

            if (classStoryAttribute == null)
            {
                StoryText = null;
                return;
            }

            StoryText = new StoryText(classStoryAttribute.GetStoryText());
        }

        private static StoryAttribute FindStoryAttribute()
        {
            if (new StackTrace().GetFrames()
                .FirstOrDefault(frame =>
                    frame.GetMethod()?.DeclaringType
                        ?.GetCustomAttribute(typeof(StoryAttribute), true) is StoryAttribute)?.GetMethod()?.DeclaringType
                ?.GetCustomAttribute(typeof(StoryAttribute), true) is StoryAttribute classStoryAttribute)
            {
                return classStoryAttribute;
            }

            return null;
        }

        private void SetScenarioText()
        {
            var stackFrames = new StackTrace().GetFrames();

            var stepAttributeFrame = stackFrames.FirstOrDefault(it => GetScenarioTextAttribute(it) != null);
            if (stepAttributeFrame != null)
            {
                ScenarioText = new ScenarioText($"{GetScenarioTextAttribute(stepAttributeFrame)}");
                return;
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == _callerMember);
            if (callingFrame != null)
            {
                ScenarioText = new ScenarioText($"{callingFrame.GetMethod().Name.Humanize()}");
                return;
            }

            ScenarioText = new ScenarioText("No Scenario Text found (Use attribute [ScenarioText\"...\")] on your tests");
        }

        private static string GetScenarioTextAttribute(StackFrame it)
        {
            return GetScenarioTextAttribute(it.GetMethod());
        }

        private static string GetScenarioTextAttribute(ICustomAttributeProvider it)
        {
            return ((ScenarioTextAttribute)((it.GetCustomAttributes(typeof(ScenarioTextAttribute), true) ??
                                             new string[] { }).FirstOrDefault()))?.Text;
        }

        [JsonProperty]
        public List<Step> Steps { get; set; }

        [JsonProperty]
        public string Output { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty]
        public Status Status { get; set; } = Status.Inconclusive;

        [JsonConverter(typeof(TimespanConverter))]
        [JsonProperty]
        public TimeSpan TimeTaken;

        [JsonIgnore]
        private readonly string _callerMember;

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
