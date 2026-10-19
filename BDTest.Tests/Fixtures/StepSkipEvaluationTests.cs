using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Helpers;
using BDTest.Test.Steps;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StepSkipEvaluationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Rules_run_once_after_all_before_hooks_and_never_during_text_refresh(bool customText)
    {
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.StepHooks.Add(new Hook { Before = _ => { calls.Add("global before"); return Task.CompletedTask; } });
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ =>
        {
            calls.Add("predicate");
            return calls.Contains("instance before");
        });
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run) { Before = _ => { calls.Add("instance before"); return Task.CompletedTask; } };
        var prefix = fixture.When(Skippable);
        if (customText) prefix = prefix.WithStepText(() => "custom");
        var builder = prefix.Then(NoOp);
        await Assert.That(calls).IsEmpty();
        var scenario = await builder.BDTestAsync();
        var step = run.Store.ScenariosByInternalId[scenario.Guid].ExecutionSteps[0];
        step.SetStepText();
        step.SetStepText();
        await Assert.That(calls).IsEquivalentTo(new[] { "global before", "instance before", "predicate", "global before", "instance before" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(step.Status).IsEqualTo(Status.Skipped);
    }

    [Test]
    public async Task Predicate_failure_is_recorded_and_survives_after_hook_failure()
    {
        var failure = new InvalidOperationException("predicate failed");
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => throw failure);
        var afterCalls = 0;
        BDTest.Contracts.StepResult? observed = null;
        options.StepHooks.Add(new Hook { After = step =>
        {
            afterCalls++;
            observed = step;
            throw new ArgumentException("after failed");
        } });
        using var run = BDTestRun.Start(options);
        var thrown = await Assert.That(async () =>
            await new Fixture(run).When(Skippable).Then(NoOp).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(afterCalls).IsEqualTo(1);
        await Assert.That(Required(observed).StartTime).IsNotEqualTo(default(DateTime));
        await Assert.That(Required(observed).EndTime).IsGreaterThanOrEqualTo(Required(observed).StartTime);
        await Assert.That(Required(observed).Output).Contains("After-step hook failed");
        var scenario = run.Scenarios.Single();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(Required(scenario.Steps)[0].Exception).Message).IsEqualTo(failure.Message);
        await Assert.That(Required(scenario.Steps)[1].Status).IsEqualTo(Status.NotRun);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Branches_share_cached_metadata_but_not_skip_results(bool generatedMetadata, bool customText)
    {
        var evaluations = 0;
        var skip = true;
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => { evaluations++; return skip; });
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run);
        var prefix = generatedMetadata ? fixture.When(() => Skippable()) : fixture.When(Skippable);
        if (customText) prefix = prefix.WithStepText(() => "custom");
        var first = prefix.Then(NoOp);
        var second = prefix.Then(NoOp);
        await Assert.That(evaluations).IsZero();
        var skipped = await first.BDTestAsync();
        skip = false;
        var executed = await second.BDTestAsync();
        await Assert.That(evaluations).IsEqualTo(2);
        var skippedStep = Required(skipped.Steps)[0];
        await Assert.That(skippedStep.Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(skippedStep.SkipReason).AttributeType).IsEqualTo(typeof(ConditionalSkipAttribute).FullName);
        await Assert.That(Required(skippedStep.SkipReason).RuleIndex).IsEqualTo(0);
        await Assert.That(Required(executed.Steps)[0].Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(executed.Steps)[0].SkipReason).IsNull();
    }

    [Test]
    public async Task Retry_rechecks_condition_and_clears_the_previous_reason()
    {
        var evaluations = 0;
        var attempts = 0;
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => ++evaluations == 1);
        options.RetryTestRules.Add(_ => true, 1);
        var reasons = new List<BDTest.Contracts.StepSkipReason?>();
        options.StepHooks.Add(new Hook { After = step =>
        {
            if (step.StepType == StepType.When) reasons.Add(step.SkipReason);
            return Task.CompletedTask;
        } });
        using var run = BDTestRun.Start(options);
        var scenario = await new Fixture(run).When(Skippable).Then(() =>
        {
            if (++attempts == 1) throw new InvalidOperationException("retry");
        }).BDTestAsync();
        await Assert.That(evaluations).IsEqualTo(2);
        await Assert.That(reasons).Count().IsEqualTo(2);
        await Assert.That(reasons[0]).IsNotNull();
        await Assert.That(reasons[1]).IsNull();
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(scenario.Steps)[0].SkipReason).IsNull();
    }

    [Test]
    public async Task Registrations_after_start_do_not_change_run_rules()
    {
        var evaluations = 0;
        var options = new BDTestOptions();
        using var frozen = BDTestRun.Start(options);
        var builder = new Fixture(frozen).When(LateRegistered).Then(NoOp);
        options.SkipStepRules.Add<LateSkipAttribute>(_ => { evaluations++; return true; });
        var scenario = await builder.BDTestAsync();
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Passed);
        await Assert.That(evaluations).IsZero();
    }

    [Test]
    public async Task Attributes_are_constructed_once_per_definition_across_attempts()
    {
        CountingSkipAttribute.Constructions = 0;
        var options = new BDTestOptions();
        options.SkipStepRules.Add<CountingSkipAttribute>(attribute => attribute.Enabled);
        using var run = BDTestRun.Start(options);
        var runnable = new Runnable((Action)CachedAttribute);
        var step = new Step(runnable, StepType.When, "scenario", "report", run);
        await Assert.That(CountingSkipAttribute.Constructions).IsZero();
        await step.Execute(null);
        step.ResetData();
        await step.Execute(null);
        await Assert.That(CountingSkipAttribute.Constructions).IsEqualTo(1);
        await Assert.That(step.Status).IsEqualTo(Status.Skipped);
    }

    [Test]
    [Arguments(StepType.When, true)]
    [Arguments(StepType.AndWhen, true)]
    [Arguments(StepType.ButWhen, true)]
    [Arguments(StepType.Given, false)]
    [Arguments(StepType.Then, false)]
    public async Task Debug_skips_record_the_setting_only_for_when_steps(StepType type, bool skipped)
    {
        var options = new BDTestOptions();
        options.Debug.ShouldSkipWhenStep = true;
        using var run = BDTestRun.Start(options);
        var step = new Step(new Runnable((Action)NoOp), type, "scenario", "report", run);
        await step.Execute(null);
        await Assert.That(step.Status).IsEqualTo(skipped ? Status.Skipped : Status.Passed);
        await Assert.That(step.SkipReason?.Source).IsEqualTo(skipped ? "DebugSetting" : null);
        await Assert.That(step.SkipReason?.Setting).IsEqualTo(skipped ? "Debug.ShouldSkipWhenStep" : null);
    }

    [Test]
    public async Task Skip_reasons_round_trip_and_historical_reports_have_no_reason()
    {
        var step = new Step(StepType.When) { SkipReason = new StepSkipReason("Rule", "<unsafe>", 2) };
        var restored = ReportJson.DeserializeObject<Step>(ReportJson.SerializeObject(step));
        await Assert.That(Required(restored.SkipReason).Source).IsEqualTo("Rule");
        await Assert.That(Required(restored.SkipReason).AttributeType).IsEqualTo("<unsafe>");
        await Assert.That(Required(restored.SkipReason).RuleIndex).IsEqualTo(2);
        await Assert.That(ReportJson.DeserializeObject<Step>("{}").SkipReason).IsNull();
    }

    [Test]
    public async Task Reports_display_and_encode_skip_reasons()
    {
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => true);
        using var run = BDTestRun.Start(options);
        var scenario = await new Fixture(run).When(Skippable).Then(NoOp).BDTestAsync();
        var step = run.Store.ScenariosByInternalId[scenario.Guid].ExecutionSteps[0];
        // A report may come from an untrusted producer.
        step.SkipReason = new StepSkipReason("Rule", "<script>unsafe</script>", 0);
        var restored = Required(Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run))).Scenarios).Single();
        await Assert.That(Required(Required(restored.Steps)[0].SkipReason).AttributeType).IsEqualTo("<script>unsafe</script>");
        foreach (var html in new[]
        {
            BDTestHtmlHelper.GetTestHtmlReport(null, run),
            await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", run.Store.ScenariosByInternalId[restored.Guid])
        })
        {
            await Assert.That(html).Contains("Skip rule 0 matched &lt;script&gt;unsafe&lt;/script&gt;.");
            await Assert.That(html).DoesNotContain("<script>unsafe</script>");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Failed_before_hooks_and_unreached_steps_do_not_evaluate_rules(bool failBefore)
    {
        var evaluations = 0;
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => { evaluations++; return true; });
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run)
        {
            Before = _ => failBefore ? throw new InvalidOperationException("before") : Task.CompletedTask
        };
        await Assert.That(async () =>
            await fixture.When(() => { throw new InvalidOperationException("body"); }).Then(Skippable).BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(evaluations).IsZero();
        await Assert.That(Required(run.Scenarios.Single().Steps)[1].SkipReason).IsNull();
    }

    [Test]
    public async Task First_matching_rule_is_recorded_and_later_rules_are_not_evaluated()
    {
        var evaluations = new List<int>();
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => { evaluations.Add(0); return false; });
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => { evaluations.Add(1); return true; });
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => { evaluations.Add(2); return true; });
        options.Debug.ShouldSkipWhenStep = true;
        using var run = BDTestRun.Start(options);
        var scenario = await new Fixture(run).When(Skippable).Then(NoOp).BDTestAsync();
        await Assert.That(evaluations).IsEquivalentTo(new[] { 0, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(Required(scenario.Steps)[0].SkipReason).Source).IsEqualTo("Rule");
        await Assert.That(Required(Required(scenario.Steps)[0].SkipReason).RuleIndex).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Inherited_skip_if_attributes_preserve_their_condition(bool generatedMetadata)
    {
        var options = new BDTestOptions();
        options.SkipStepRules.Add<SkipStepIfAttribute>(attribute => attribute.Environment == "test");
        using var run = BDTestRun.Start(options);
        var fixture = new Fixture(run);
        var steps = new InheritedSteps();
        var builder = generatedMetadata ? fixture.When(() => steps.Conditional()) : fixture.When(steps.Conditional);
        var scenario = await builder.Then(NoOp).BDTestAsync();
        var step = Required(scenario.Steps)[0];
        await Assert.That(step.Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(step.SkipReason).AttributeType).IsEqualTo(typeof(SkipStepIfAttribute).FullName);
    }

    private class BaseSteps
    {
        [SkipStepIf("test")]
        public virtual void Conditional() => throw new InvalidOperationException("must be skipped");
    }
    private sealed class InheritedSteps : BaseSteps
    {
        public override void Conditional() => base.Conditional();
    }
    private sealed class SkipStepIfAttribute(string environment) : SkipStepAttribute
    {
        public string Environment { get; } = environment;
    }

    [LateSkip]
    private static void LateRegistered() { }
    private sealed class LateSkipAttribute : SkipStepAttribute;
    [CountingSkip(true)]
    private static void CachedAttribute() { }
    private sealed class CountingSkipAttribute : SkipStepAttribute
    {
        internal static int Constructions;
        public bool Enabled { get; }
        public CountingSkipAttribute(bool enabled) { Constructions++; Enabled = enabled; }
    }

    [ConditionalSkip]
    private static void Skippable() { }
    private static void NoOp() { }
    private sealed class ConditionalSkipAttribute : SkipStepAttribute;

    private sealed class Hook : IStepHook
    {
        public Func<BDTest.Contracts.StepResult, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<BDTest.Contracts.StepResult, Task> After { get; init; } = _ => Task.CompletedTask;
        public Task BeforeStepAsync(BDTest.Contracts.StepResult step) => Before(step);
        public Task AfterStepAsync(BDTest.Contracts.StepResult step) => After(step);
    }

    private sealed class Fixture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public Func<BDTest.Contracts.StepResult, Task> Before { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeStep(BDTest.Contracts.StepResult step) => Before(step);
    }
}
