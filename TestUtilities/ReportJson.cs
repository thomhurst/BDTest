using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Helpers;

namespace BDTest.TestUtilities;

// Use production generated metadata for report types, with reflection only for anonymous test inputs.
internal static class ReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(BDTestJsonContext.Default,
            ReportJsonContext.Default, new DefaultJsonTypeInfoResolver())
    };

    public static string SerializeObject<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T DeserializeObject<T>(string json) => typeof(T) == typeof(BDTestOutputModel)
        ? (T)(object)BDTestJsonHelper.Deserialize(json)
        : JsonSerializer.Deserialize<T>(json, Options);
}
