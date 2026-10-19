using BDTest.Contracts;
using BDTest.Maps;
using System.Text.Json;
using System.Threading;

namespace BDTest.Helpers;

public static class BDTestJsonHelper
{
    private const long DefaultMaximumExpandedSize = 100 * 1024 * 1024;

    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    /// <param name="bdTestRunDescriptor">Optional export metadata; null uses default metadata.</param>
    /// <param name="run">The caller-owned run. Export after tests and teardown finish, before disposing it.</param>
    /// <returns>Indented, schema-versioned JSON. Read it with <see cref="DeserializeReport"/>.</returns>
    public static string GetTestJsonData(BDTestRunDescriptor? bdTestRunDescriptor, BDTestRun run)
    {
        var dataToOutput = BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor);
        return Serialize(dataToOutput, writeIndented: true);
    }

    private static readonly BDTestReportJsonContext ReportIndentedContext = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    /// <summary>Writes an immutable, schema-versioned report using generated contract metadata.</summary>
    public static string Serialize(BDTestReport model) => Serialize(model, writeIndented: false);

    /// <summary>Writes an immutable, schema-versioned report with explicit formatting.</summary>
    public static string Serialize(BDTestReport model, bool writeIndented) =>
        JsonSerializer.Serialize(model, (writeIndented ? ReportIndentedContext : BDTestReportJsonContext.Default).BDTestReport);

    /// <summary>Reads the current schema. Historical unversioned reports use the legacy Deserialize API.</summary>
    public static BDTestReport DeserializeReport(string json) => BDTestReportJson.Deserialize(json);

    private static readonly BDTestJsonContext IndentedContext = new(new JsonSerializerOptions
    {
        WriteIndented = true
    });

    /// <summary>Writes plain report JSON using generated metadata, without reference preservation.</summary>
    public static string Serialize(BDTestOutputModel? model) => Serialize(model, writeIndented: false);

    /// <summary>Writes plain legacy report JSON with explicit formatting.</summary>
    public static string Serialize(BDTestOutputModel? model, bool writeIndented)
    {
        return JsonSerializer.Serialize(model, (writeIndented ? IndentedContext : BDTestJsonContext.Default).BDTestOutputModel);
    }

    /// <summary>Reads current reports and historical Newtonsoft reports with reference metadata.</summary>
    public static BDTestOutputModel? Deserialize(string json, long maximumExpandedSize = DefaultMaximumExpandedSize)
    {
        if (maximumExpandedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpandedSize));
        }
        using var document = JsonDocument.Parse(json);
        return LegacyReportJson.Deserialize(document.RootElement, maximumExpandedSize);
    }

    /// <summary>Reads a UTF-8 report stream without closing the caller's stream.</summary>
    /// <param name="stream">The report input.</param>
    /// <param name="cancellationToken">Cancels reading the input.</param>
    /// <param name="maximumExpandedSize">Maximum UTF-8 bytes after expanding legacy references. Current JSON is not expanded.</param>
    public static async Task<BDTestOutputModel?> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default,
        long maximumExpandedSize = DefaultMaximumExpandedSize)
    {
        if (maximumExpandedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpandedSize));
        }
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return LegacyReportJson.Deserialize(document.RootElement, maximumExpandedSize);
    }
}
