using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BDTest.Output;

internal class TimespanConverter : JsonConverter<TimeSpan>
{
    /// <summary>The historical report duration format, including seven fractional second digits.</summary>
    public static string TimeSpanFormatString { get; } = @"d\.hh\:mm\:ss\:fffffff";

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(TimeSpanFormatString, CultureInfo.InvariantCulture));
    }

    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !TimeSpan.TryParseExact(reader.GetString(), TimeSpanFormatString, CultureInfo.InvariantCulture, out var duration))
        {
            throw new JsonException($"Expected a duration string in format '{TimeSpanFormatString}'.");
        }

        return duration;
    }
}
