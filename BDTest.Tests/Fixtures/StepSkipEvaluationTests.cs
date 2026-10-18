using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Helpers;
using BDTest.Test.Steps;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StepSkipEvaluationTests
{
    [TestCase(false)]
    [TestCase(true)]
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
        Assert.That(calls, Is.Empty);
        var scenario = await builder.BDTestAsync();
        var step = Required(scenario.Steps)[0];
        step.SetStepText();
        step.SetStepText();
        Assert.That(calls, Is.EqualTo(new[] { "global before", "instance before", "predicate", "global before", "instance before" }));
        Assert.That(step.Status, Is.EqualTo(Status.Skipped));
    }

    [Test]
    public async Task Predicate_failure_is_recorded_and_survives_after_hook_failure()
    {
        var failure = new InvalidOperationException("predicate failed");
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => throw failure);
        var afterCalls = 0;
        Step? observed = null;
        options.StepHooks.Add(new Hook { After = step =>
        {
            afterCalls++;
            observed = step;
            throw new ArgumentException("after failed");
        } });
        using var run = BDTestRun.Start(options);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new Fixture(run).When(Skippable).Then(NoOp).BDTestAsync());
        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(afterCalls, Is.EqualTo(1));
        Assert.That(Required(observed).StartTime, Is.Not.EqualTo(default(DateTime)));
        Assert.That(Required(observed).EndTime, Is.GreaterThanOrEqualTo(Required(observed).StartTime));
        Assert.That(Required(observed).Output, Does.Contain("After-step hook failed"));
        var scenario = run.Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(Required(scenario.Steps)[0].Exception).Message, Is.EqualTo(failure.Message));
        Assert.That(Required(scenario.Steps)[1].Status, Is.EqualTo(Status.NotRun));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
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
        Assert.That(evaluations, Is.Zero);
        var skipped = await first.BDTestAsync();
        skip = false;
        var executed = await second.BDTestAsync();
        Assert.That(evaluations, Is.EqualTo(2));
        var skippedStep = Required(skipped.Steps)[0];
        Assert.That(skippedStep.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(skippedStep.SkipReason).AttributeType, Is.EqualTo(typeof(ConditionalSkipAttribute).FullName));
        Assert.That(Required(skippedStep.SkipReason).RuleIndex, Is.Zero);
        Assert.That(Required(executed.Steps)[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(executed.Steps)[0].SkipReason, Is.Null);
    }

    [Test]
    public async Task Retry_rechecks_condition_and_clears_the_previous_reason()
    {
        var evaluations = 0;
        var attempts = 0;
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => ++evaluations == 1);
        options.RetryTestRules.Add(_ => true, 1);
        var reasons = new List<StepSkipReason?>();
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
        Assert.That(evaluations, Is.EqualTo(2));
        Assert.That(reasons, Has.Count.EqualTo(2));
        Assert.That(reasons[0], Is.Not.Null);
        Assert.That(reasons[1], Is.Null);
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(scenario.Steps)[0].SkipReason, Is.Null);
    }

    [Test]
    public async Task Legacy_registration_after_construction_is_live_but_explicit_runs_are_frozen()
    {
        var evaluations = 0;
        var legacy = new Fixture(BDTestRun.Default).When(LateRegistered).Then(NoOp);
        BDTestSettings.GlobalSkipStepRules.Add<LateSkipAttribute>(_ => { evaluations++; return true; });
        Assert.That(evaluations, Is.Zero);
        var legacyScenario = await legacy.BDTestAsync();
        Assert.That(evaluations, Is.EqualTo(1));
        Assert.That(Required(legacyScenario.Steps)[0].Status, Is.EqualTo(Status.Skipped));

        var options = new BDTestOptions();
        using var frozen = BDTestRun.Start(options);
        var builder = new Fixture(frozen).When(LateRegistered).Then(NoOp);
        options.SkipStepRules.Add<LateSkipAttribute>(_ => { evaluations++; return true; });
        var scenario = await builder.BDTestAsync();
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(evaluations, Is.EqualTo(1));
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
        Assert.That(CountingSkipAttribute.Constructions, Is.Zero);
        await step.Execute(null);
        step.ResetData();
        await step.Execute(null);
        Assert.That(CountingSkipAttribute.Constructions, Is.EqualTo(1));
        Assert.That(step.Status, Is.EqualTo(Status.Skipped));
    }

    [TestCase(StepType.When, true)]
    [TestCase(StepType.AndWhen, true)]
    [TestCase(StepType.ButWhen, true)]
    [TestCase(StepType.Given, false)]
    [TestCase(StepType.Then, false)]
    public async Task Debug_skips_record_the_setting_only_for_when_steps(StepType type, bool skipped)
    {
        var options = new BDTestOptions();
        options.Debug.ShouldSkipWhenStep = true;
        using var run = BDTestRun.Start(options);
        var step = new Step(new Runnable((Action)NoOp), type, "scenario", "report", run);
        await step.Execute(null);
        Assert.That(step.Status, Is.EqualTo(skipped ? Status.Skipped : Status.Passed));
        Assert.That(step.SkipReason?.Source, Is.EqualTo(skipped ? "DebugSetting" : null));
        Assert.That(step.SkipReason?.Setting, Is.EqualTo(skipped ? "Debug.ShouldSkipWhenStep" : null));
    }

    [Test]
    public void Skip_reasons_round_trip_and_historical_reports_have_no_reason()
    {
        var step = new Step(StepType.When) { SkipReason = new StepSkipReason("Rule", "<unsafe>", 2) };
        var restored = ReportJson.DeserializeObject<Step>(ReportJson.SerializeObject(step));
        Assert.That(Required(restored.SkipReason).Source, Is.EqualTo("Rule"));
        Assert.That(Required(restored.SkipReason).AttributeType, Is.EqualTo("<unsafe>"));
        Assert.That(Required(restored.SkipReason).RuleIndex, Is.EqualTo(2));
        Assert.That(ReportJson.DeserializeObject<Step>("{}").SkipReason, Is.Null);
    }

    [Test]
    public async Task Reports_display_and_encode_skip_reasons()
    {
        var options = new BDTestOptions();
        options.SkipStepRules.Add<ConditionalSkipAttribute>(_ => true);
        using var run = BDTestRun.Start(options);
        var scenario = await new Fixture(run).When(Skippable).Then(NoOp).BDTestAsync();
        var step = Required(scenario.Steps)[0];
        // A report may come from an untrusted producer.
        step.SkipReason = new StepSkipReason("Rule", "<script>unsafe</script>", 0);
        var restored = Required(Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run))).Scenarios).Single();
        Assert.That(Required(Required(restored.Steps)[0].SkipReason).AttributeType, Is.EqualTo("<script>unsafe</script>"));
        foreach (var html in new[]
        {
            BDTestHtmlHelper.GetTestHtmlReport(null, run),
            await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored)
        })
        {
            Assert.That(html, Does.Contain("Skip rule 0 matched &lt;script&gt;unsafe&lt;/script&gt;."));
            Assert.That(html, Does.Not.Contain("<script>unsafe</script>"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
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
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.When(() => { throw new InvalidOperationException("body"); }).Then(Skippable).BDTestAsync());
        Assert.That(evaluations, Is.Zero);
        Assert.That(Required(run.Scenarios.Single().Steps)[1].SkipReason, Is.Null);
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
        Assert.That(evaluations, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(Required(Required(scenario.Steps)[0].SkipReason).Source, Is.EqualTo("Rule"));
        Assert.That(Required(Required(scenario.Steps)[0].SkipReason).RuleIndex, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
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
        Assert.That(step.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(step.SkipReason).AttributeType, Is.EqualTo(typeof(SkipStepIfAttribute).FullName));
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
        public Func<Step, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<Step, Task> After { get; init; } = _ => Task.CompletedTask;
        public Task BeforeStepAsync(Step step) => Before(step);
        public Task AfterStepAsync(Step step) => After(step);
    }

    private sealed class Fixture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public Func<Step, Task> Before { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeStep(Step step) => Before(step);
    }
}
