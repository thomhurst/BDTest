using System.Text.Json;
using System.Text.Json.Serialization;
using BDTest.Test;

namespace BDTest.Helpers.JsonConverters;

internal class StoryTextConverter : JsonConverter<StoryText>
{
    public override void Write(Utf8JsonWriter writer, StoryText value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Story);
    }

    public override StoryText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a report text string.");
        }
        return new StoryText(reader.GetString());
    }
}
