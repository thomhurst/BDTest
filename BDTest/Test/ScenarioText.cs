using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class ScenarioText : IEquatable<ScenarioText>
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

        public bool Equals(ScenarioText other)
        {
            if (other == null)
            {
                return false;
            }

            return ReferenceEquals(this, other) || string.Equals(Scenario, other.Scenario);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((ScenarioText) obj);
        }

        public override int GetHashCode()
        {
            return Scenario != null ? Scenario.GetHashCode() : 0;
        }
    }
}
