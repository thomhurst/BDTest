using BDTest.Contracts;

namespace BDTest.Razor.Reports.Storage;

/// <summary>Persistent run metadata, independent of Razor view models.</summary>
public sealed class RunIndexEntry
{
    public string? RecordId { get; set; } = "";
    public DateTime StartedAtDateTime { get; set; }
    public DateTime FinishedAtDateTime { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(BDTest.Razor.Reports.Helpers.ReportStatusConverter))]
    public Status Status { get; set; }
    public RunIndexCounts? Counts { get; set; } = new();
    public string? Tag { get; set; }
    public string? Environment { get; set; }
    public string? BranchName { get; set; }
    public string? MachineName { get; set; }
    public string? Version { get; set; }
}

public sealed class RunIndexCounts
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Inconclusive { get; set; }
    public int NotImplemented { get; set; }
    public int Skipped { get; set; }
    public int NotRun { get; set; }
    public int Pending { get; set; }
}
