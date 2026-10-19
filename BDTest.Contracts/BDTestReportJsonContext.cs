using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>Generated metadata for schema-versioned reports. Legacy reports require a compatibility reader.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BDTestReport))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0041", Justification = "System.Text.Json generates nullable-oblivious JsonTypeInfo properties; their signatures remain checked by the public API baseline.")]
public partial class BDTestReportJsonContext : JsonSerializerContext
{
}
