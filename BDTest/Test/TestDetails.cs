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
        private readonly string _callerMember;
        public string CallerFile { get; }
        public string TestId { get; }
        public IEnumerable<string> Parameters { get; set; }
        internal int StepCount { get; set; }

        internal TestDetails(string callerMember, string callerFile, Guid guid, string testId)
        {
            TestDetails = this;
            Guid = guid;
            _callerMember = callerMember;
            CallerFile = callerFile;
            TestId = testId;
            SetStoryText();
            SetScenarioText();
            StepCount = 1;
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
                SetParameters(stepAttributeFrame);
                ScenarioText = new ScenarioText($"{GetScenarioTextAttribute(stepAttributeFrame)}");
                return;
            }

            var callingFrame = stackFrames.FirstOrDefault(it => it.GetMethod().Name == _callerMember);
            if (callingFrame != null)
            {
                SetParameters(callingFrame);
                ScenarioText = new ScenarioText($"{callingFrame.GetMethod().Name.Humanize()}");
                return;
            }

            ScenarioText = new ScenarioText("No Scenario Text found (Use attribute [ScenarioText\"...\")] on your tests");
        }

        private void SetParameters(StackFrame callingFrame)
        {
            Parameters = callingFrame?.GetMethod()?.GetParameters().Select(it => it.Name);
        }

        private static string GetScenarioTextAttribute(StackFrame it)
        {
            return GetScenarioTextAttribute(it.GetMethod());
        }

        private static string GetScenarioTextAttribute(ICustomAttributeProvider it)
        {
            return ((ScenarioTextAttribute)(it.GetCustomAttributes(typeof(ScenarioTextAttribute), true) ??
                                            new string[] { }).FirstOrDefault())?.Text;
        }

        public Guid GetGuid()
        {
            return Guid;
        }
    }
}
