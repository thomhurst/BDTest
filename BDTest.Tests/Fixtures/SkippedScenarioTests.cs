using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
[BDTestRetry(2)]
public class SkippedScenarioTests : RunTestBase
{
    [SetUp]
    public void RegisterStepRule() => Options.SkipStepRules.Add<SkippedProbeAttribute>(_ => true);

    [Test]
    public async Task AllSkippedStepsProduceSkippedScenario()
    {
        var scenario = await Given(() => SkippedStep()).When(() => SkippedStep())
            .Then(() => SkippedStep()).BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(scenario.Steps).Select(s => s.Status), Is.All.EqualTo(Status.Skipped));
        Assert.That(scenario.RetryCount, Is.Zero);
        Assert.That(scenario.EndTime, Is.GreaterThanOrEqualTo(scenario.StartTime));
    }

    [Test]
    public async Task PartiallySkippedStepsProducePassedScenario()
    {
        var scenario = await Given(() => SkippedStep()).When(() => ExecutedStep())
            .Then(() => SkippedStep()).BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(scenario.Steps).Select(s => s.Status),
            Is.EqualTo(new[] { Status.Skipped, Status.Passed, Status.Skipped }));
    }

    [Test]
    public async Task DebugWhenSkipDoesNotSkipWholeScenario()
    {
        Options.Debug.ShouldSkipWhenStep = true;
        var scenario = await Given(() => ExecutedStep()).When(() => MustNotRun())
            .Then(() => ExecutedStep()).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(scenario.Steps).Select(s => s.Status),
            Is.EqualTo(new[] { Status.Passed, Status.Skipped, Status.Passed }));

    }

    [Test]
    public void CustomSkipBypassesRetryRulesAndPreservesExecutedSteps()
    {
        Options.SkipExceptionTypes.Add(typeof(ProbeSkipException));
        Options.RetryTestRules.Add(e => e is ProbeSkipException, 3);
        var builder = Given(() => ExecutedStep()).When(() => ThrowSkip()).Then(() => MustNotRun());

        Assert.ThrowsAsync<ProbeSkipException>(async () => await builder.BDTestAsync());
        var scenario = BDTestUtil.GetScenarios(TestRun).Single(s => s.Guid == builder.Guid);
        Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(scenario.Steps).Select(s => s.Status),
            Is.EqualTo(new[] { Status.Passed, Status.Skipped, Status.Skipped }));
        Assert.That(scenario.RetryCount, Is.Zero);
        Assert.That(scenario.Exception, Is.Null);
        Assert.That(Required(scenario.Steps).All(s => s.Runnable == null), Is.True);
    }

    [Test]
    public void CustomSkipFormatsRemainingStepTextBeforeReleasingReferences()
    {
        Options.SkipExceptionTypes.Add(typeof(ProbeSkipException));
        var formatted = 0;
        var builder = Given(() => ExecutedStep()).When(() => ThrowSkip())
            .Then(() => MustNotRun()).WithStepText(() => $"custom skipped step {++formatted}")
            .And(() => MustNotRun()).WithStepText(() => $"another skipped step {++formatted}");

        Assert.ThrowsAsync<ProbeSkipException>(async () => await builder.BDTestAsync());
        var scenario = BDTestUtil.GetScenarios(TestRun).Single(s => s.Guid == builder.Guid);
        Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(scenario.Steps).Skip(2).Select(s => s.StepText),
            Is.EqualTo(new[] { "Then custom skipped step 1", "And another skipped step 2" }));
        Assert.That(Required(scenario.Steps).Skip(2).Select(s => s.Status), Is.All.EqualTo(Status.Skipped));
        Assert.That(formatted, Is.EqualTo(2));
        Assert.That(Required(scenario.Steps).All(s => s.Runnable == null && s.OverriddenStepText == null), Is.True);
    }

    private static void ThrowSkip() => throw new ProbeSkipException();
    private sealed class ProbeSkipException : Exception { }

    [SkippedProbe]
    private static void SkippedStep() => MustNotRun();
    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
    private static void ExecutedStep() { }
    private sealed class SkippedProbeAttribute : SkipStepAttribute { }
}
