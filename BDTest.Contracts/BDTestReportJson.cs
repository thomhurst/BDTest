using System.Text.Json;

namespace BDTest.Contracts;

/// <summary>Reads and writes the current report schema without a dependency on test execution.</summary>
public static class BDTestReportJson
{
    public static string Serialize(BDTestReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        return JsonSerializer.Serialize(report, BDTestReportJsonContext.Default.BDTestReport);
    }

    /// <summary>Rejects missing and unsupported schema versions. Unversioned reports require a legacy reader.</summary>
    public static BDTestReport Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var schemaVersion)
            || schemaVersion != BDTestReport.CurrentSchemaVersion)
        {
            throw new JsonException("Expected a report with schemaVersion " + BDTestReport.CurrentSchemaVersion + ".");
        }
        return root.Deserialize(BDTestReportJsonContext.Default.BDTestReport)
            ?? throw new JsonException("Expected a report object.");
    }
}
