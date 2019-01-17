using Newtonsoft.Json;

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
