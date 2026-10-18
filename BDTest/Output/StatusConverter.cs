using BDTest.Test;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BDTest.Output;

/// <summary>Writes status names and reads numeric values using the historical report status mapping.</summary>
public sealed class StatusConverter : JsonConverter<Status>
{
    private static readonly JsonConverter<Status> NamedConverter = (JsonConverter<Status>)
        new JsonStringEnumConverter<Status>(allowIntegerValues: false).CreateConverter(typeof(Status), new JsonSerializerOptions());

    public override Status Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (!reader.TryGetInt64(out var legacyStatus))
            {
                throw new JsonException("Expected an integer legacy status.");
            }
            return legacyStatus switch
            {
                0 => Status.Passed,
                1 => Status.Failed,
                2 => Status.Inconclusive,
                3 => Status.NotImplemented,
                4 => Status.Skipped,
                _ => throw new JsonException("Unknown legacy status.")
            };
        }
        return NamedConverter.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, Status value, JsonSerializerOptions options)
        => NamedConverter.Write(writer, value, options);
}
