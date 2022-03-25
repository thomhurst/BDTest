using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Helpers.JsonConverters;

public class ScenarioTextConverter : JsonConverter<ScenarioText>
{
    public override void WriteJson(JsonWriter writer, ScenarioText value, JsonSerializer serializer)
    {
        if (value == null)
        {
            return;
        }
            
        writer.WriteValue(value.Scenario);
    }

    public override ScenarioText ReadJson(JsonReader reader, Type objectType, ScenarioText existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        return new ScenarioText(reader.Value?.ToString());
    }
}