using System.Text.Json;
using BDTest.Contracts;

namespace BDTest.Razor.Reports.Serialization;

/// <summary>Reads current and historical reports without loading the execution library.</summary>
public static class HostedReportJson
{
    private const long DefaultMaximumExpandedSize = 100 * 1024 * 1024;

    /// <summary>Dispatches by schemaVersion; missing versions use bounded legacy reference expansion.</summary>
    public static BDTestReport? Deserialize(string json, long maximumExpandedSize = DefaultMaximumExpandedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumExpandedSize);
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement, maximumExpandedSize);
    }

    /// <summary>Reads UTF-8 report JSON without closing the caller's stream.</summary>
    public static async Task<BDTestReport?> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default,
        long maximumExpandedSize = DefaultMaximumExpandedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumExpandedSize);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Read(document.RootElement, maximumExpandedSize);
    }

    private static BDTestReport? Read(JsonElement root, long maximumExpandedSize)
    {
        if (root.ValueKind == JsonValueKind.Null) return null;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a report object.");
        try
        {
            if (!root.TryGetProperty("schemaVersion", out var version))
                return LegacyReportJson.Deserialize(root, maximumExpandedSize);
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schemaVersion)
                || schemaVersion != BDTestReport.CurrentSchemaVersion)
                throw new JsonException("Unsupported report schema version.");
            // ProducerVersion is descriptive only; schemaVersion selects the reader.
            return root.Deserialize(BDTestReportJsonContext.Default.BDTestReport);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Invalid report data.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new JsonException("Invalid report data.", exception);
        }
        catch (FormatException exception)
        {
            throw new JsonException("Invalid report data.", exception);
        }
    }
}
