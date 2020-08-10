using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class TestDetails : BuildableTest
    {
        [JsonProperty]
        public string CallerMember { get; set; }
        [JsonProperty]
        public string CallerFile { get; set; }
        [JsonProperty]
        public string TestId { get; set; }

        [JsonIgnore]
        public BDTestBase BdTestBase { get; }

        [JsonProperty]
        public IEnumerable<string> Parameters { get; set; }

        internal TestDetails(string callerMember, string callerFile, Guid guid, string testId, BDTestBase bdTestBase)
        {
            // TODO RESTRUCTURE
            TestDetails = null;
            Guid = guid.ToString();
            CallerMember = callerMember;
            CallerFile = callerFile;
            TestId = testId;
            BdTestBase = bdTestBase;
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

        private StoryAttribute FindStoryAttribute()
        {
            return BdTestBase.GetType().GetCustomAttribute(typeof(StoryAttribute)) as StoryAttribute;
        }

        private void SetScenarioText()
        {
            ScenarioText = ScenarioTextHelper.GetScenarioText(CallerMember, out var parameters);
            Parameters = parameters;
        }

        private static StackFrame[] GetStackFrames()
        {
            return new StackTrace().GetFrames() ?? Array.Empty<StackFrame>();
        }

        private static IEnumerable<TestInformationAttribute> GetTestInformationAttribute(StackFrame it)
        {
            return GetBDTestInformationAttributes(it.GetMethod());
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
