using BDTest.Contracts;

namespace BDTest.Razor.Reports.Helpers;

internal static class ReportResults
{
    public static ReportTimer GetTestTimer(IReadOnlyCollection<ScenarioResult> scenarios, BDTestReport? report = null) =>
        scenarios.Count == 0 && report?.TestTimer != null ? report.TestTimer : new(
            scenarios.Select(scenario => scenario.StartTime).Where(time => time != default).DefaultIfEmpty().Min(),
            scenarios.Select(scenario => scenario.EndTime).Where(time => time != default).DefaultIfEmpty().Max());

    public static BDTestReport WithStorageIdentity(BDTestReport report, string id, string? attachmentTokenHash) => new(
        report.SchemaVersion, id, report.ProducerVersion, report.Scenarios, report.NotRun,
        report.Environment, report.Tag, report.BranchName, report.MachineName, report.TestTimer,
        report.CustomProperties, attachmentTokenHash);

    public static int GetOrder(this Status status) => status switch
    {
        Status.Failed => 1,
        Status.NotImplemented => 2,
        Status.Inconclusive => 3,
        Status.Skipped => 4,
        Status.NotRun => 5,
        Status.Pending => 6,
        Status.Passed => 7,
        _ => int.MaxValue
    };
}
