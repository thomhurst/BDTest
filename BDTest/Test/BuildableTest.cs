using System;
using BDTest.Attributes;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class BuildableTest
    {
        [JsonIgnore]
        protected string Guid { get; set; }

        [JsonProperty] internal StoryText StoryText { get; set; }

        [JsonProperty]
        internal ScenarioText ScenarioText { get; set; }

        [JsonProperty]
        public TestDetails TestDetails { get; protected set; }
        
        [JsonProperty]
        public TestInformationAttribute[] CustomTestInformation { get; set; } = Array.Empty<TestInformationAttribute>();

        public string GetScenarioText()
        {
            return ScenarioText?.Scenario ?? "Scenario Text Not Defined";
        }

        public string GetStoryText()
        {
            return StoryText?.Story ?? "Story Text Not Defined";
        }
    }
}
