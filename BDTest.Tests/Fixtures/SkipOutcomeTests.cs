using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

// These tests deliberately reach the runner as skips. Test-SkipOutcomes.ps1 checks
// the TRX outcomes, while the finally block checks BDTest's own recorded statuses.
[NonParallelizable]
[BDTestRetry(2)]
public class SkipOutcomeTests : BDTest.NUnit.NUnitBDTestBase<object>
{
    static SkipOutcomeTests()
    {
        BDTestSettings.GlobalSkipTestRules.Add(s =>
            s.BdTestBaseClass is SkipOutcomeTests && s.GetScenarioText() == "Global skip probe");
        BDTestSettings.GlobalSkipStepRules.Add<SkipProbeAttribute>(_ => true);
    }

    [Test]
    [ScenarioText("Global skip probe")]
    public Task GlobalRuleSkips()
    {
        return CheckScenario(() => Given(() => MustNotRun()).When(() => MustNotRun())
            .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped,
            Status.Skipped, Status.Skipped, Status.Skipped);
    }

    [Test]
    [ScenarioText("Global skip probe")]
    public Task GlobalRuleSkipsWithDirectAwait()
    {
        return CheckScenario(async () => await Given(() => MustNotRun()).When(() => MustNotRun())
            .Then(() => MustNotRun()), Status.Skipped,
            Status.Skipped, Status.Skipped, Status.Skipped);
    }

    [Test]
    public Task AllStepsSkip() => CheckScenario(() => Given(() => SkippedStep()).When(() => SkippedStep())
        .Then(() => SkippedStep()).BDTestAsync(), Status.Skipped,
        Status.Skipped, Status.Skipped, Status.Skipped);

    [Test]
    public Task PartialStepsPass() => CheckScenario(() => Given(() => ExecutedStep()).When(() => SkippedStep())
        .Then(() => ExecutedStep()).BDTestAsync(), Status.Passed,
        Status.Passed, Status.Skipped, Status.Passed);

    [Test]
    public Task FrameworkSkipInStep() => CheckScenario(() => Given(() => ExecutedStep()).When(() => Assert.Ignore("probe"))
        .Then(() => MustNotRun()).BDTestAsync(), Status.Skipped,
        Status.Passed, Status.Skipped, Status.Skipped);

    [Test]
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
            var scenario = BDTestUtil.GetScenarios().Single(s => s.FrameworkTestId == id);
            if (scenario.Status != status || !scenario.Steps.Select(s => s.Status).SequenceEqual(stepStatuses)
                || scenario.RetryCount != 0 || scenario.EndTime < scenario.StartTime || scenario.Exception != null)
            {
                throw new InvalidOperationException($"Unexpected BDTest result: {scenario.Status}, " +
                    $"steps: {string.Join(", ", scenario.Steps.Select(s => s.Status))}, retries: {scenario.RetryCount}");
            }
        }
    }

    [SkipProbe]
    private static void SkippedStep() => MustNotRun();
    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
    private static void ExecutedStep() { }
    private sealed class SkipProbeAttribute : SkipStepAttribute { }
}
