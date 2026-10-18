using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Extensions;

public static class RunIndexExtensions
{
    public static RunIndexEntry ToRunIndexEntry(this TestRunSummary entry) => new()
    {
        RecordId = entry.RecordId,
        StartedAtDateTime = entry.StartedAtDateTime,
        FinishedAtDateTime = entry.FinishedAtDateTime,
        Status = entry.Status,
        Tag = entry.Tag,
        Environment = entry.Environment,
        BranchName = entry.BranchName,
        MachineName = entry.MachineName,
        Version = entry.Version,
        Counts = new RunIndexCounts
        {
            Total = entry.Counts?.Total ?? 0,
            Passed = entry.Counts?.Passed ?? 0,
            Failed = entry.Counts?.Failed ?? 0,
            Inconclusive = entry.Counts?.Inconclusive ?? 0,
            NotImplemented = entry.Counts?.NotImplemented ?? 0,
            Skipped = entry.Counts?.Skipped ?? 0,
            NotRun = entry.Counts?.NotRun ?? 0,
            Pending = entry.Counts?.Pending ?? 0,
        }
    };

    public static TestRunSummary ToTestRunSummary(this RunIndexEntry entry) => new()
    {
        RecordId = entry.RecordId,
        StartedAtDateTime = entry.StartedAtDateTime,
        FinishedAtDateTime = entry.FinishedAtDateTime,
        Status = entry.Status,
        Tag = entry.Tag,
        Environment = entry.Environment,
        BranchName = entry.BranchName,
        MachineName = entry.MachineName,
        Version = entry.Version,
        Counts = new Counts
        {
            Total = entry.Counts?.Total ?? 0,
            Passed = entry.Counts?.Passed ?? 0,
            Failed = entry.Counts?.Failed ?? 0,
            Inconclusive = entry.Counts?.Inconclusive ?? 0,
            NotImplemented = entry.Counts?.NotImplemented ?? 0,
            Skipped = entry.Counts?.Skipped ?? 0,
            NotRun = entry.Counts?.NotRun ?? 0,
            Pending = entry.Counts?.Pending ?? 0,
        }
    };

}
