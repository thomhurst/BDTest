using BDTest.Contracts;

namespace BDTest.Helpers;

internal static class BDTestOutputModelBuilder
{
    internal static BDTestReport Build(BDTestRun run, BDTestRunDescriptor? descriptor)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        var snapshot = run.GetSnapshot();
        return new BDTestReport(BDTestReport.CurrentSchemaVersion, snapshot.ReportId, snapshot.Version,
            snapshot.Scenarios, snapshot.NotRun, descriptor?.Environment, descriptor?.Tag,
            descriptor?.BranchName, Environment.MachineName, snapshot.Timer, descriptor?.CustomProperties);
    }
}
