using Newtonsoft.Json;

namespace BDTest.Output;

public class TimespanConverter : JsonConverter<TimeSpan>
{
    /// <summary>
    /// Format: Days.Hours:Minutes:Seconds:Milliseconds
    /// </summary>
    public static string TimeSpanFormatString { get; } = @"d\.hh\:mm\:ss\:fffffff";

    public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
    {
        var timespanFormatted = $"{value.ToString(TimeSpanFormatString)}";
        writer.WriteValue(timespanFormatted);
    }

    /// <summary>Reads a duration string in <see cref="TimeSpanFormatString"/> format. Null and invalid values are rejected.</summary>
    /// <exception cref="JsonSerializationException">The token is not a string or does not contain a valid duration.</exception>
    public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String
            || !TimeSpan.TryParseExact((string)reader.Value, TimeSpanFormatString, null, out var parsedTimeSpan))
        {
            throw new JsonSerializationException($"Expected a duration string in format '{TimeSpanFormatString}'. Path '{reader.Path}'.");
        }

        return parsedTimeSpan;
    }
}
