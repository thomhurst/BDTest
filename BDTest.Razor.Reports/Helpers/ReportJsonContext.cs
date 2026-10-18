using System.Text.Json.Serialization;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Helpers;

/// <summary>Generated metadata for stored report summaries.</summary>
[JsonSerializable(typeof(TestRunSummary))]
public partial class ReportJsonContext : JsonSerializerContext
{
}
