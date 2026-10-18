using BDTest.Test;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BDTest.Output;

// New reports use names. Integer tokens belong to the enum used by legacy reports.
internal sealed class StatusConverter : StringEnumConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Integer)
        {
            return Convert.ToInt64(reader.Value) switch
            {
                0 => Status.Passed,
                1 => Status.Failed,
                2 => Status.Inconclusive,
                3 => Status.NotImplemented,
                4 => Status.Skipped,
                _ => throw new JsonSerializationException($"Unknown legacy status: {reader.Value}.")
            };
        }
        return base.ReadJson(reader, objectType, existingValue, serializer);
    }
}
