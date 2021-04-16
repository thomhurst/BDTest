using System;
using System.Collections.Generic;
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
            SetScenarioText();
            StoryText = StoryTextHelper.GetStoryText(bdTestBase);
            CustomTestInformation = TestInformationAttributeHelper.GetTestInformationAttributes();
        }

        [JsonConstructor]
        private TestDetails()
        {

        }

        private void SetScenarioText()
        {
            var scenarioTextWithParameters = ScenarioTextHelper.GetScenarioTextWithParameters(CallerMember);
            ScenarioText = scenarioTextWithParameters.ScenarioText;
            Parameters = scenarioTextWithParameters.Parameters;
        }

        public string GetGuid()
        {
            return Guid;
        }
    }
}
