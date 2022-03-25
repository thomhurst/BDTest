using System;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models;

public class TestRunSummary
{
    [JsonConstructor]
    private TestRunSummary()
    {
    }
        
    internal TestRunSummary(BDTestOutputModel bdTestOutputModel)
    {
        RecordId = bdTestOutputModel.Id;
        StartedAtDateTime = bdTestOutputModel.TestTimer.TestsStartedAt;
        FinishedAtDateTime = bdTestOutputModel.TestTimer.TestsFinishedAt;
        Status = bdTestOutputModel.Scenarios.GetTotalStatus();
        Tag = bdTestOutputModel.Tag;
        Environment = bdTestOutputModel.Environment;
        Version = bdTestOutputModel.Version;
        MachineName = bdTestOutputModel.MachineName;
        BranchName = bdTestOutputModel.BranchName;

        SetCounts(bdTestOutputModel);
    }

    private void SetCounts(BDTestOutputModel bdTestOutputModel)
    {
        Counts.Total = bdTestOutputModel.Scenarios.Count;
        Counts.NotRun = bdTestOutputModel.NotRun?.Count ?? 0;

        foreach (var scenario in bdTestOutputModel.Scenarios)
        {
            switch (scenario.Status)
            {
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
        
    [JsonProperty]
    public string RecordId { get; set; }
    [JsonProperty]
    public DateTime StartedAtDateTime { get; set; }
    [JsonProperty]
    public DateTime FinishedAtDateTime { get; set; }
    [JsonProperty]
    public Status Status { get; set; }
    [JsonProperty]
    public Counts Counts { get; set; } = new();
    [JsonProperty]
    public string Tag { get; set; }
    [JsonProperty]
    public string Environment { get; set; }
    [JsonProperty]
    public string BranchName { get; set; }
    [JsonProperty]
    public string MachineName { get; set; }
    [JsonProperty]
    public string Version { get; set; }
}

public class Counts
{
    [JsonProperty]
    public int Total { get; set; }
    [JsonProperty]
    public int Passed { get; set; }
    [JsonProperty]
    public int Failed { get; set; }
    [JsonProperty]
    public int Inconclusive { get; set; }
    [JsonProperty]
    public int NotImplemented { get; set; }
    [JsonProperty]
    public int Skipped { get; set; }
    [JsonProperty]
    public int NotRun { get; set; }
}