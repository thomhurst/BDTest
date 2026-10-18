using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Test.Steps.When;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class AndWhenTests : BDTestBase
{
    [Test]
    public async Task And_when_accepts_all_delegate_shapes_and_keeps_execution_order()
    {
        var calls = new List<string>();
        Action action = () => calls.Add("action");
        Func<Task> task = async () => { await Task.Yield(); calls.Add("task"); };
        Func<CancellationToken, Task> tokenTask = async token =>
        {
            Assert.That(token.CanBeCanceled, Is.True);
            await Task.Yield();
            calls.Add("token task");
        };
        Func<ValueTask> valueTask = async () => { await Task.Yield(); calls.Add("value task"); };
        Func<CancellationToken, ValueTask> tokenValueTask = async token =>
        {
            Assert.That(token.CanBeCanceled, Is.True);
            await Task.Yield();
            calls.Add("token value task");
        };
        Func<ValueTask<int>> result = async () => { await Task.Yield(); calls.Add("result"); return 42; };
        Func<CancellationToken, ValueTask<int>> tokenResult = async token =>
        {
            Assert.That(token.CanBeCanceled, Is.True);
            await Task.Yield();
            calls.Add("token result");
            return 42;
        };
        var scenario = await Given(() => NoOp()).When(() => NoOp())
            .And(action).And(task).And(tokenTask).WithStepTimeout(TimeSpan.FromSeconds(10))
            .And(valueTask).And(tokenValueTask).WithStepTimeout(TimeSpan.FromSeconds(10))
            .And(result).And(tokenResult).WithStepTimeout(TimeSpan.FromSeconds(10))
            .But(() => NoOp()).And(async () => { await Task.Yield(); calls.Add("async lambda"); })
            .And(async token => { await Task.Yield(); calls.Add("async token lambda"); })
            .Then(() => calls.Add("then")).BDTestAsync();

        Assert.That(calls, Is.EqualTo(new[]
        {
            "action", "task", "token task", "value task", "token value task", "result", "token result",
            "async lambda", "async token lambda", "then"
        }));
        Assert.That(scenario.Steps.All(step => step.Status == Status.Passed), Is.True);
        Assert.That(scenario.Steps.Count(step => step.StepType == StepType.AndWhen), Is.EqualTo(9));
    }

    [Test]
    public async Task Generated_metadata_and_lazy_arguments_render_and_when_text()
    {
        var value = "initial";
        var reads = 0;
        var when = When(() => NoOp());
        AndWhen generated = when.And(() => NamedStep("generated"));
        AndWhen arguments = generated.And(() => NamedStep(value)).WithArgs(() => { reads++; return value; });
        AndWhen overridden = arguments.And(() => NoOp()).WithStepText(() => "custom action");
        Assert.That(reads, Is.Zero);
        value = "executed";

        var scenario = await overridden.Then(() => NoOp()).BDTestAsync();

        Assert.That(reads, Is.EqualTo(1));
        Assert.That(scenario.Steps.Select(step => step.StepText), Is.EqualTo(new[]
        {
            "When no op", "And named generated", "And named executed", "And custom action", "Then no op"
        }));
    }

    [Test]
    public async Task And_continuations_from_one_when_have_independent_steps_and_text()
    {
        var calls = new List<string>();
        var when = When(() => calls.Add("when"));
        var first = when.And(() => calls.Add("first")).WithStepText(() => "first action").Then(() => NoOp());
        var second = when.And(() => calls.Add("second")).WithStepText(() => "second action").Then(() => NoOp());
        var firstScenario = await first.BDTestAsync();
        var secondScenario = await second.BDTestAsync();

        Assert.That(calls, Is.EqualTo(new[] { "when", "first", "when", "second" }));
        Assert.That(firstScenario.Guid, Is.Not.EqualTo(secondScenario.Guid));
        Assert.That(firstScenario.Steps, Has.Count.EqualTo(3));
        Assert.That(secondScenario.Steps, Has.Count.EqualTo(3));
        Assert.That(firstScenario.Steps[1].StepText, Is.EqualTo("And first action"));
        Assert.That(secondScenario.Steps[1].StepText, Is.EqualTo("And second action"));
    }

    [Test]
    public async Task And_when_obeys_legacy_debug_skipping()
    {
        var previous = BDTestSettings.Debug.ShouldSkipWhenStep;
        try
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = true;
            var calls = new List<string>();
            var scenario = await When(() => calls.Add("when")).And(() => calls.Add("and"))
                .Then(() => calls.Add("then")).BDTestAsync();
            Assert.That(calls, Is.EqualTo(new[] { "then" }));
            Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.Skipped));
            Assert.That(scenario.Steps[1].StepText, Does.StartWith("[Skipped] And "));
        }
        finally
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = previous;
        }
    }

    [Test]
    public async Task And_when_uses_frozen_debug_options_from_its_own_run()
    {
        var options = new BDTestOptions();
        options.Debug.ShouldSkipWhenStep = true;
        using var skipped = BDTestRun.Start(options);
        using var plain = BDTestRun.Start();
        options.Debug.ShouldSkipWhenStep = false;
        var calls = new List<string>();
        var first = await new Capture(skipped).When(() => NoOp()).And(() => calls.Add("skipped"))
            .Then(() => NoOp()).BDTestAsync();
        var second = await new Capture(plain).When(() => NoOp()).And(() => calls.Add("plain"))
            .Then(() => NoOp()).BDTestAsync();

        Assert.That(calls, Is.EqualTo(new[] { "plain" }));
        Assert.That(first.Steps[1].Status, Is.EqualTo(Status.Skipped));
        Assert.That(second.Steps[1].Status, Is.EqualTo(Status.Passed));
    }

    [Test]
    public async Task And_when_round_trips_and_renders_in_static_and_hosted_reports()
    {
        using var run = BDTestRun.Start();
        var scenario = await new Capture(run).When(() => NoOp()).And(() => NamedStep("report"))
            .Then(() => NoOp()).BDTestAsync();
        var restored = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run)).Scenarios.Single();
        Assert.That(restored.Steps[1].StepType, Is.EqualTo(StepType.AndWhen));
        Assert.That(restored.Steps[1].StepText, Is.EqualTo("And named report"));
        Assert.That(BDTestHtmlHelper.GetTestHtmlReport(null, run), Does.Contain("<span class=\"kw\">And</span><span class=\"step-text\">named report</span>"));
        Assert.That(await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored),
            Does.Contain("<b>And</b> named report"));
    }

    private static void NoOp() { }

    [StepText("named {0}")]
    private static void NamedStep(string value) { }

    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
