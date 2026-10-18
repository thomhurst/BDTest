using BDTest.Maps;
using System.Text.Json;
using System.Threading;

namespace BDTest.Helpers;

public static class BDTestJsonHelper
{
    private const long DefaultMaximumExpandedSize = 100 * 1024 * 1024;

    public static string GetTestJsonData()
    {
        return GetTestJsonData(new BDTestRunDescriptor());
    }

    public static string GetTestJsonData(BDTestRunDescriptor? bdTestRunDescriptor) => GetTestJsonData(bdTestRunDescriptor, BDTestRun.Default);

    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    public static string GetTestJsonData(BDTestRunDescriptor? bdTestRunDescriptor, BDTestRun run)
    {
        var dataToOutput = BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor);
        return Serialize(dataToOutput, writeIndented: true);
    }

    private static readonly BDTestJsonContext IndentedContext = new(new JsonSerializerOptions
    {
        WriteIndented = true
    });

    /// <summary>Writes plain report JSON using generated metadata, without reference preservation.</summary>
    public static string Serialize(BDTestOutputModel? model, bool writeIndented = false)
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
