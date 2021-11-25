using BDTest.Helpers.JsonConverters;
using Newtonsoft.Json;

namespace BDTest.Test
{
    [JsonConverter(typeof(ScenarioTextConverter))]
    public record ScenarioText
    {
        [JsonProperty] public string Scenario { get; private set; }

        public ScenarioText(string scenario)
        {
            Scenario = scenario;
        }

        [JsonConstructor]
        private ScenarioText()
        {
        }
    }
}
