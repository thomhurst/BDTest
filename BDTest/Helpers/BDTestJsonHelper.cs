using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using System.Text.Json;
using System.Threading;

namespace BDTest.Helpers;

public static class BDTestJsonHelper
{
    public static string GetTestJsonData()
    {
        return GetTestJsonData(new BDTestRunDescriptor());
    }

    public static string GetTestJsonData(BDTestRunDescriptor bdTestRunDescriptor) => GetTestJsonData(bdTestRunDescriptor, BDTestRun.Default);

    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    public static string GetTestJsonData(BDTestRunDescriptor bdTestRunDescriptor, BDTestRun run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
            
        var testTimer = GetTestTimer(scenarios, run);
            
        var dataToOutput = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.LegacyEnvironment,
            Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.LegacyTag,
            BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.LegacyBranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            TestTimer = testTimer,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            Version = BDTestVersionHelper.CurrentVersion
        };
            
        return Serialize(dataToOutput, writeIndented: true);
    }

    private static readonly BDTestJsonContext IndentedContext = new(new JsonSerializerOptions
    {
        WriteIndented = true
    });

    /// <summary>Writes plain report JSON using generated metadata, without reference preservation.</summary>
    public static string Serialize(BDTestOutputModel model, bool writeIndented = false)
    {
        return JsonSerializer.Serialize(model, (writeIndented ? IndentedContext : BDTestJsonContext.Default).BDTestOutputModel);
    }

    /// <summary>Reads current reports and historical Newtonsoft reports with reference metadata.</summary>
    public static BDTestOutputModel Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        return LegacyReportJson.Deserialize(document.RootElement);
    }

    /// <summary>Reads a UTF-8 report stream without closing the caller's stream.</summary>
    /// <param name="stream">The report input.</param>
    /// <param name="cancellationToken">Cancels reading the input.</param>
    /// <param name="maximumExpandedSize">Maximum UTF-8 bytes after expanding legacy references. Current JSON is not expanded.</param>
    public static async Task<BDTestOutputModel> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default,
        long maximumExpandedSize = long.MaxValue)
    {
        if (maximumExpandedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpandedSize));
        }
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return LegacyReportJson.Deserialize(document.RootElement, maximumExpandedSize);
    }

    private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios, BDTestRun run)
    {
        var enumerable = scenarios.ToList();

        if (enumerable.Count == 0)
        {
            return new TestTimer { TestsStartedAt = run.StartedAt };
        }

        var testTimer = new TestTimer
        {
            TestsStartedAt = enumerable.GetStartDateTime(),
            TestsFinishedAt = enumerable.GetEndDateTime()
        };

        return testTimer;
    }
}
