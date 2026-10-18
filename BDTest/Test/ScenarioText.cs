using BDTest.Helpers.JsonConverters;
using System.Text.Json.Serialization;

namespace BDTest.Test;

[JsonConverter(typeof(ScenarioTextConverter))]
public record ScenarioText
{
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