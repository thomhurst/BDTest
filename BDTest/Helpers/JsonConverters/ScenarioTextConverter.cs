using System.Text.Json;
using System.Text.Json.Serialization;
using BDTest.Test;

namespace BDTest.Helpers.JsonConverters;

public class ScenarioTextConverter : JsonConverter<ScenarioText>
{
    public override void Write(Utf8JsonWriter writer, ScenarioText value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Scenario);
    }

    public override ScenarioText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a report text string.");
        }
        return new ScenarioText(reader.GetString());
    }
}
