using Newtonsoft.Json;

namespace BDTest.Test
{
    public class ScenarioText
    {
        [JsonProperty]
        public string Scenario { get; private set; }

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
