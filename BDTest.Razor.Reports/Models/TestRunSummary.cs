using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Test;
using System.Text.Json.Serialization;

namespace BDTest.Razor.Reports.Models;

public class TestRunSummary
{
    [JsonConstructor]
    internal TestRunSummary()
    {
    }
        
    internal TestRunSummary(BDTestOutputModel bdTestOutputModel)
    {
        RecordId = bdTestOutputModel.Id;
        StartedAtDateTime = bdTestOutputModel.TestTimer?.TestsStartedAt ?? default;
        FinishedAtDateTime = bdTestOutputModel.TestTimer?.TestsFinishedAt ?? default;
        Status = (bdTestOutputModel.Scenarios ?? Enumerable.Empty<Scenario>()).GetTotalStatus();
        Tag = bdTestOutputModel.Tag;
        Environment = bdTestOutputModel.Environment;
        Version = bdTestOutputModel.Version;
        MachineName = bdTestOutputModel.MachineName;
        BranchName = bdTestOutputModel.BranchName;

        SetCounts(bdTestOutputModel);
    }

    private void SetCounts(BDTestOutputModel bdTestOutputModel)
    {
        var counts = Counts ??= new();
        counts.Total = bdTestOutputModel.Scenarios?.Count ?? 0;
        counts.NotRun = bdTestOutputModel.NotRun?.Count ?? 0;

        foreach (var scenario in bdTestOutputModel.Scenarios ?? Enumerable.Empty<Scenario>())
        {
            switch (scenario.Status)
            {
                case Status.NotRun:
                    counts.NotRun++;
                    break;
                case Status.Pending:
                    counts.Pending++;
                    break;
                case Status.Passed:
                    counts.Passed++;
                    break;
                case Status.Failed:
                    counts.Failed++;
                    break;
                case Status.Inconclusive:
                    counts.Inconclusive++;
                    break;
                case Status.NotImplemented:
                    counts.NotImplemented++;
                    break;
                case Status.Skipped:
                    counts.Skipped++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
        
    [JsonInclude]
    public string? RecordId { get; set; }
    [JsonInclude]
    public DateTime StartedAtDateTime { get; set; }
    [JsonInclude]
    public DateTime FinishedAtDateTime { get; set; }
    [JsonInclude]
    [System.Text.Json.Serialization.JsonConverter(typeof(BDTest.Razor.Reports.Helpers.ReportStatusConverter))]
    public Status Status { get; set; }
    [JsonInclude]
    public Counts? Counts { get; set; } = new();
    [JsonInclude]
    public string? Tag { get; set; }
    [JsonInclude]
    public string? Environment { get; set; }
    [JsonInclude]
    public string? BranchName { get; set; }
    [JsonInclude]
    public string? MachineName { get; set; }
    [JsonInclude]
    public string? Version { get; set; }
}

public class Counts
{
    [JsonInclude]
    public int Total { get; set; }
    [JsonInclude]
    public int Passed { get; set; }
    [JsonInclude]
    public int Failed { get; set; }
    [JsonInclude]
    public int Inconclusive { get; set; }
    [JsonInclude]
    public int NotImplemented { get; set; }
    [JsonInclude]
    public int Skipped { get; set; }
    [JsonInclude]
    public int NotRun { get; set; }
    [JsonInclude]
    public int Pending { get; set; }
}
