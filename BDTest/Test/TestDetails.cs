using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BDTest.Attributes;
using Humanizer;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class TestDetails : BuildableTest
    {
        [JsonProperty]
        public string CallerMember;
        [JsonProperty]
        public string CallerFile { get; }
        [JsonProperty]
        public string TestId { get; }
        [JsonProperty]
        public IEnumerable<string> Parameters { get; set; }

        internal TestDetails(string callerMember, string callerFile, Guid guid, string testId)
        {
            // TODO RESTRUCTURE
            TestDetails = null;
            Guid = guid.ToString();
            CallerMember = callerMember;
            CallerFile = callerFile;
            TestId = testId;
            SetStoryText();
            SetScenarioText();
            SetTestInformation();
        }

        private void SetTestInformation()
        {
            var stackFrame = GetStackFrames().FirstOrDefault(it =>
            {
                var testInformationAttribute = GetTestInformationAttribute(it);
                return testInformationAttribute != null && testInformationAttribute.Any();
            });

            if (stackFrame != null)
            {
                CustomTestInformation = GetTestInformationAttribute(stackFrame)?.ToArray() ?? Array.Empty<TestInformationAttribute>();
            }
        }

        [JsonConstructor]
        private TestDetails()
        {

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
            if (GetStackFrames()
                .FirstOrDefault(frame => frame.GetMethod()?.DeclaringType
                    ?.GetCustomAttribute(typeof(StoryAttribute), true) is StoryAttribute)?.GetMethod()?.DeclaringType
                ?.GetCustomAttribute(typeof(StoryAttribute), true) is StoryAttribute classStoryAttribute)
            {
                return classStoryAttribute;
            }

            return null;
        }

        private void SetScenarioText()
        {
            var stackFrames = GetStackFrames();

            var stepAttributeFrame = stackFrames.FirstOrDefault(it => GetScenarioTextAttribute(it) != null);
            if (stepAttributeFrame != null)
            {
                SetParameters(stepAttributeFrame);
                ScenarioText = new ScenarioText($"{GetScenarioTextAttribute(stepAttributeFrame)}");
                return;
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == CallerMember);
            if (callingFrame != null)
            {
                SetParameters(callingFrame);
                ScenarioText = new ScenarioText($"{callingFrame.GetMethod().Name.Humanize()}");
                return;
            }

            ScenarioText = new ScenarioText("No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests");
        }

        private static StackFrame[] GetStackFrames()
        {
            return new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();
        }

        private void SetParameters(StackFrame callingFrame)
        {
            Parameters = callingFrame?.GetMethod()?.GetParameters().Select(it => it.Name);
        }

        private static string GetScenarioTextAttribute(StackFrame it)
        {
            return GetScenarioTextAttribute(it.GetMethod());
        }
        
        private static IEnumerable<TestInformationAttribute> GetTestInformationAttribute(StackFrame it)
        {
            return GetBDTestInformationAttributes(it.GetMethod());
        }

        private static string GetScenarioTextAttribute(ICustomAttributeProvider it)
        {
            return (it.GetCustomAttributes(typeof(ScenarioTextAttribute), true).FirstOrDefault() as ScenarioTextAttribute)?.Text;
        }
        
        private static IEnumerable<TestInformationAttribute> GetBDTestInformationAttributes(ICustomAttributeProvider it)
        {
            return it.GetCustomAttributes(typeof(TestInformationAttribute), true) as TestInformationAttribute[] ?? Enumerable.Empty<TestInformationAttribute>();
        }

        public string GetGuid()
        {
            return Guid;
        }
    }
}
