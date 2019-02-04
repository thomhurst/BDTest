using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class ScenarioText : IEquatable<ScenarioText>
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

        public bool Equals(ScenarioText other)
        {
            return Scenario == other.Scenario;
        }
    }
}
