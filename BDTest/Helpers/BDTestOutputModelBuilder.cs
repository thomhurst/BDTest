using BDTest.Maps;
using BDTest.Output;

namespace BDTest.Helpers;

internal static class BDTestOutputModelBuilder
{
    internal static BDTestOutputModel Build(BDTestRun run, BDTestRunDescriptor? descriptor)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
        var model = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = descriptor?.Environment,
            Tag = descriptor?.Tag,
            BranchName = descriptor?.BranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            Version = BDTestVersionHelper.CurrentVersion,
            TestTimer = scenarios.Count == 0
                ? new TestTimer { TestsStartedAt = run.StartedAt, TestsFinishedAt = run.Options.Clock.UtcNow }
                : BDTestUtil.GetTestTimer(scenarios)
        };

        if (descriptor != null)
        {
            var customProperties = model.CustomProperties ?? throw new InvalidOperationException("The report has no custom properties collection.");
            foreach (var property in descriptor.CustomProperties)
                customProperties.Add(property.Key, property.Value);
        }

        return model;
    }
}
