using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Attributes;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class BuildableTest
    {
        [JsonIgnore]
        internal string Guid { get; set; }

        [JsonProperty] 
        internal StoryText StoryText { get; set; }

        [JsonProperty]
        internal ScenarioText ScenarioText { get; set; }

        [JsonProperty] 
        public IEnumerable<string> Parameters = Enumerable.Empty<string>();

        [JsonProperty]
        public TestInformationAttribute[] CustomTestInformation { get; set; } = Array.Empty<TestInformationAttribute>();
        
        [JsonProperty]
        public string CallerMember { get; protected set; }
        [JsonProperty]
        public string CallerFile { get; protected set; }
        [JsonProperty]
        public string TestId { get; protected set; }
        [JsonIgnore]
        public BDTestBase BdTestBase { get; protected set; }

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
