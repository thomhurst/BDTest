using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BDTest.Test
{
    public class ScenarioText
    {
        [JsonProperty]
        public string Scenario;

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
