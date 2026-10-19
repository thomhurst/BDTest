using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[BDTestRetry(2)]
public class SkippedScenarioTests : RunTestBase
{
    [Before(HookType.Test)]
    public void RegisterStepRule() => Options.SkipStepRules.Add<SkippedProbeAttribute>(_ => true);

    [Test]
    public async Task AllSkippedStepsProduceSkippedScenario()
    {
        var scenario = await Given(() => SkippedStep()).When(() => SkippedStep())
            .Then(() => SkippedStep()).BDTestAsync();

        await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);

        await Assert.That((Required(scenario.Steps).Select(s => s.Status)).All(item => Equals(item, Status.Skipped))).IsTrue();
        await Assert.That(scenario.RetryCount).IsZero();
        await Assert.That(scenario.EndTime).IsGreaterThanOrEqualTo(scenario.StartTime);
    }

    [Test]
    public async Task PartiallySkippedStepsProducePassedScenario()
    {
        var scenario = await Given(() => SkippedStep()).When(() => ExecutedStep())
            .Then(() => SkippedStep()).BDTestAsync();

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(scenario.Steps).Select(s => s.Status)).IsEquivalentTo(new[] { Status.Skipped, Status.Passed, Status.Skipped }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DebugWhenSkipDoesNotSkipWholeScenario()
    {
        Options.Debug.ShouldSkipWhenStep = true;
        var scenario = await Given(() => ExecutedStep()).When(() => MustNotRun())
            .Then(() => ExecutedStep()).BDTestAsync();
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(scenario.Steps).Select(s => s.Status)).IsEquivalentTo(new[] { Status.Passed, Status.Skipped, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

    }

    [Test]
    public async Task CustomSkipBypassesRetryRulesAndPreservesExecutedSteps()
    {
        Options.SkipExceptionTypes.Add(typeof(ProbeSkipException));
        Options.RetryTestRules.Add(e => e is ProbeSkipException, 3);
        var builder = Given(() => ExecutedStep()).When(() => ThrowSkip()).Then(() => MustNotRun());

        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<ProbeSkipException>();
        var scenario = BDTestUtil.GetScenarios(TestRun).Single(s => s.Guid == builder.Guid);
        await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(scenario.Steps).Select(s => s.Status)).IsEquivalentTo(new[] { Status.Passed, Status.Skipped, Status.Skipped }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.RetryCount).IsZero();
        await Assert.That(scenario.Exception).IsNull();
        await Assert.That(Required(scenario.Steps).All(s => s.Runnable == null)).IsTrue();
    }

    [Test]
    public async Task CustomSkipFormatsRemainingStepTextBeforeReleasingReferences()
    {
        Options.SkipExceptionTypes.Add(typeof(ProbeSkipException));
        var formatted = 0;
        var builder = Given(() => ExecutedStep()).When(() => ThrowSkip())
            .Then(() => MustNotRun()).WithStepText(() => $"custom skipped step {++formatted}")
            .And(() => MustNotRun()).WithStepText(() => $"another skipped step {++formatted}");

        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<ProbeSkipException>();
        var scenario = BDTestUtil.GetScenarios(TestRun).Single(s => s.Guid == builder.Guid);
        await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(scenario.Steps).Skip(2).Select(s => s.StepText)).IsEquivalentTo((new[] { "Then custom skipped step 1", "And another skipped step 2" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That((Required(scenario.Steps).Skip(2).Select(s => s.Status)).All(item => Equals(item, Status.Skipped))).IsTrue();
        await Assert.That(formatted).IsEqualTo(2);
        await Assert.That(Required(scenario.Steps).All(s => s.Runnable == null && s.OverriddenStepText == null)).IsTrue();
    }

    private static void ThrowSkip() => throw new ProbeSkipException();
    private sealed class ProbeSkipException : Exception { }

    [SkippedProbe]
    private static void SkippedStep() => MustNotRun();
    private static void MustNotRun() => throw new InvalidOperationException("Skipped step executed");
    private static void ExecutedStep() { }
    private sealed class SkippedProbeAttribute : SkipStepAttribute { }
}
