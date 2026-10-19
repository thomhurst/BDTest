using BDTest.Contracts;

namespace BDTest.Helpers;

internal static class BDTestOutputModelBuilder
{
    internal static BDTestReport Build(BDTestRun run, BDTestRunDescriptor? descriptor)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
        return new BDTestReport(BDTestReport.CurrentSchemaVersion, run.ReportId, BDTestVersionHelper.CurrentVersion ?? "unknown",
            scenarios, BDTestUtil.GetNotRunScenarios(run), descriptor?.Environment, descriptor?.Tag,
            descriptor?.BranchName, Environment.MachineName,
            scenarios.Count == 0
                ? new ReportTimer(ResultSnapshot.Utc(run.StartedAt), ResultSnapshot.Utc(run.Options.Clock.UtcNow))
                : BDTestUtil.GetTestTimer(scenarios), descriptor?.CustomProperties);
    }
}
