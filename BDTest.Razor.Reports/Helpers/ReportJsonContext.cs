using System.Text.Json.Serialization;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Helpers;

/// <summary>Generated metadata for stored report summaries.</summary>
[JsonSerializable(typeof(TestRunSummary))]
[JsonSerializable(typeof(RunIndexEntry))]
public partial class ReportJsonContext : JsonSerializerContext
{
}
