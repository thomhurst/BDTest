using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>Generated metadata for schema-versioned reports. Legacy reports require a compatibility reader.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BDTestReport))]
public partial class BDTestReportJsonContext : JsonSerializerContext
{
}
