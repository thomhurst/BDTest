using System.Text.Json.Serialization;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Helpers;

/// <summary>Generated metadata for stored report summaries.</summary>
[JsonSourceGenerationOptions(Converters = new[] { typeof(ReportStatusConverter) })]
[JsonSerializable(typeof(TestRunSummary))]
[JsonSerializable(typeof(RunIndexEntry))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0041", Justification = "System.Text.Json generates nullable-oblivious JsonTypeInfo properties; their signatures remain checked by the public API baseline.")]
public partial class ReportJsonContext : JsonSerializerContext
{
}
