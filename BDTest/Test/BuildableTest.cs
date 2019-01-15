using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class BuildableTest
    {
        [JsonIgnore]
        protected Guid Guid;

        [JsonProperty]
        public StoryText StoryText;

        [JsonProperty]
        public ScenarioText ScenarioText;

        [JsonProperty]
        public TestDetails TestDetails;

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
