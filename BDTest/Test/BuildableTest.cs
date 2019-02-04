using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class BuildableTest
    {
        [JsonIgnore]
        protected Guid Guid { get; set; }

        [JsonProperty] public StoryText StoryText { get; protected set; }

        [JsonProperty]
        public ScenarioText ScenarioText { get; protected set; }

        [JsonProperty]
        public TestDetails TestDetails { get; protected set; }

        public string GetScenarioText()
        {
            return ScenarioText?.Scenario ?? "Scenario Title Not Defined";
        }

        public string GetStoryText()
        {
            return StoryText?.Story ?? "Story Text Not Defined";
        }
    }
}
