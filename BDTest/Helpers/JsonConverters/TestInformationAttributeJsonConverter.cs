using System.Text.Json;
using System.Text.Json.Serialization;
using BDTest;

namespace BDTest.Helpers.JsonConverters;

internal class TestInformationAttributeJsonConverter : JsonConverter<TestInformationAttribute>
{
    public override void Write(Utf8JsonWriter writer, TestInformationAttribute value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Information);
    }

    public override TestInformationAttribute Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a report text string.");
        }
        return new TestInformationAttribute(reader.GetString());
    }
}
