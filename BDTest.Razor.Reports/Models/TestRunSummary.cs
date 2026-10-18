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
        Counts.Total = bdTestOutputModel.Scenarios?.Count ?? 0;
        Counts.NotRun = bdTestOutputModel.NotRun?.Count ?? 0;

        foreach (var scenario in bdTestOutputModel.Scenarios ?? Enumerable.Empty<Scenario>())
        {
            switch (scenario.Status)
            {
                case Status.NotRun:
                    Counts.NotRun++;
                    break;
                case Status.Pending:
                    Counts.Pending++;
                    break;
                case Status.Passed:
                    Counts.Passed++;
                    break;
                case Status.Failed:
                    Counts.Failed++;
                    break;
                case Status.Inconclusive:
                    Counts.Inconclusive++;
                    break;
                case Status.NotImplemented:
                    Counts.NotImplemented++;
                    break;
                case Status.Skipped:
                    Counts.Skipped++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
        
    [JsonInclude]
    public string RecordId { get; set; }
    [JsonInclude]
    public DateTime StartedAtDateTime { get; set; }
    [JsonInclude]
    public DateTime FinishedAtDateTime { get; set; }
    [JsonInclude]
    [JsonConverter(typeof(JsonStringEnumConverter<Status>))]
    public Status Status { get; set; }
    [JsonInclude]
    public Counts Counts { get; set; } = new();
    [JsonInclude]
    public string Tag { get; set; }
    [JsonInclude]
    public string Environment { get; set; }
    [JsonInclude]
    public string BranchName { get; set; }
    [JsonInclude]
    public string MachineName { get; set; }
    [JsonInclude]
    public string Version { get; set; }
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
    [JsonProperty]
    public int Pending { get; set; }
}