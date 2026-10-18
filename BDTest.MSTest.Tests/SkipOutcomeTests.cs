using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest.Tests;

// These tests deliberately reach the runner as skips. Test-SkipOutcomes.ps1 checks
// the TRX outcomes, while the finally block checks BDTest's own recorded statuses.
[BDTestClass]
[BDTestRetry(2)]
public class SkipOutcomeTests : MSTestBDTestBase<object>
{
    internal static void Configure(BDTestOptions options)
    {
        options.SkipTestRules.Add(s =>
            s.BdTestBaseClass is SkipOutcomeTests && s.GetScenarioText() == "Global skip probe");
        options.SkipStepRules.Add<SkipProbeAttribute>(_ => true);
    }

    [TestMethod]
    [ScenarioText("Global skip probe")]
    public Task GlobalRuleSkips()
    {
        return CheckScenario(() => Given(() => MustNotRun()).When(() => MustNotRun())
            .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped,
            Status.Skipped, Status.Skipped, Status.Skipped);
    }

    [TestMethod]
    [ScenarioText("Global skip probe")]
    public Task GlobalRuleSkipsWithDirectAwait()
    {
        return CheckScenario(async () => await Given(() => MustNotRun()).When(() => MustNotRun())
            .Then(() => MustNotRun()), Status.Skipped,
            Status.Skipped, Status.Skipped, Status.Skipped);
    }

    [TestMethod]
    public Task AllStepsSkip() => CheckScenario(() => Given(() => SkippedStep()).When(() => SkippedStep())
        .Then(() => SkippedStep()).BDTestAsync(), Status.Skipped,
        Status.Skipped, Status.Skipped, Status.Skipped);

    [TestMethod]
    public Task PartialStepsPass() => CheckScenario(() => Given(() => ExecutedStep()).When(() => SkippedStep())
        .Then(() => ExecutedStep()).BDTestAsync(), Status.Passed,
        Status.Passed, Status.Skipped, Status.Passed);

    [TestMethod]
    public Task GenuineInconclusive() => CheckScenario(() => Given(() => ExecutedStep()).When(() => Assert.Inconclusive("probe"))
        .Then(() => MustNotRun()).BDTestAsync(), Status.Inconclusive,
        Status.Passed, Status.Inconclusive, Status.NotRun);

    private async Task CheckScenario(Func<Task<Scenario>> run, Status status, params Status[] stepStatuses)
    {
        var id = BDTestExecutionId;
        try
        {
            await run();
        }
        finally
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(s => s.FrameworkTestId == id);
            var steps = scenario.Steps ?? throw new InvalidOperationException("Expected recorded scenario steps.");
            if (scenario.Status != status || !steps.Select(s => s.Status).SequenceEqual(stepStatuses)
                || scenario.RetryCount != 0 || scenario.EndTime < scenario.StartTime || scenario.Exception != null)
            {
                throw new InvalidOperationException($"Unexpected BDTest result: {scenario.Status}, " +
                    $"steps: {string.Join(", ", steps.Select(s => s.Status))}, retries: {scenario.RetryCount}");
            }
        }
    }

    [SkipProbe]
    private static void SkippedStep() => MustNotRun();
    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
    private static void ExecutedStep() { }
    private sealed class SkipProbeAttribute : SkipStepAttribute { }
}
