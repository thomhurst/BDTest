using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BDTest.Contracts;
using BDTest.Razor.Reports.Helpers;

namespace BDTest.TestUtilities;

// The host suite intentionally references contracts and hosting, never test execution.
internal static class ReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(BDTestReportJsonContext.Default,
            ReportJsonContext.Default, new DefaultJsonTypeInfoResolver())
    };

    public static string SerializeObject<T>(T value) => value is BDTestReport report
        ? BDTestReportJson.Serialize(report) : JsonSerializer.Serialize(value, Options);

    public static T DeserializeObject<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException($"Expected a non-null {typeof(T).Name} report value.");
}
