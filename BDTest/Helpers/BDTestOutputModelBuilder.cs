using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;

namespace BDTest.Helpers;

internal static class BDTestOutputModelBuilder
{
    internal static BDTestOutputModel Build(BDTestRun run, BDTestRunDescriptor descriptor)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
        var model = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = descriptor?.Environment ?? BDTestSettings.LegacyEnvironment,
            Tag = descriptor?.Tag ?? BDTestSettings.LegacyTag,
            BranchName = descriptor?.BranchName ?? BDTestSettings.LegacyBranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            Version = BDTestVersionHelper.CurrentVersion,
            TestTimer = scenarios.Count == 0
                ? new TestTimer { TestsStartedAt = run.StartedAt }
                : BDTestUtil.GetTestTimer(scenarios)
        };

#pragma warning disable CS0612 // Preserve the shipped setting for all report formats until its separate removal.
        foreach (var property in BDTestSettings.CustomProperties)
            model.CustomProperties.Add(property.Key, property.Value);
#pragma warning restore CS0612

        return model;
    }
}
