using BDTest;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class AndWhenTests : RunTestBase
{
    [Test]
    public async Task And_when_accepts_all_delegate_shapes_and_keeps_execution_order()
    {
        var calls = new List<string>();
        Action action = () => calls.Add("action");
        Func<Task> task = async () => { await Task.Yield(); calls.Add("task"); };
        Func<CancellationToken, Task> tokenTask = async token =>
        {
            await Assert.That(token.CanBeCanceled).IsTrue();
            await Task.Yield();
            calls.Add("token task");
        };
        Func<ValueTask> valueTask = async () => { await Task.Yield(); calls.Add("value task"); };
        Func<CancellationToken, ValueTask> tokenValueTask = async token =>
        {
            await Assert.That(token.CanBeCanceled).IsTrue();
            await Task.Yield();
            calls.Add("token value task");
        };
        Func<ValueTask<int>> result = async () => { await Task.Yield(); calls.Add("result"); return 42; };
        Func<CancellationToken, ValueTask<int>> tokenResult = async token =>
        {
            await Assert.That(token.CanBeCanceled).IsTrue();
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

        await Assert.That(calls).IsEquivalentTo(new[]
        {
            "action", "task", "token task", "value task", "token value task", "result", "token result",
            "async lambda", "async token lambda", "then"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps).All(step => step.Status == Status.Passed)).IsTrue();
        await Assert.That(scenario.Steps.Count(step => step.StepType == StepType.AndWhen)).IsEqualTo(9);
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
        await Assert.That(reads).IsZero();
        value = "executed";

        var scenario = await overridden.Then(() => NoOp()).BDTestAsync();

        await Assert.That(reads).IsEqualTo(1);
        await Assert.That(Required(scenario.Steps).Select(step => step.StepText)).IsEquivalentTo((new[]
        {
            "When no op", "And named generated", "And named executed", "And custom action", "Then no op"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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

        await Assert.That(calls).IsEquivalentTo(new[] { "when", "first", "when", "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(firstScenario.Guid).IsNotEqualTo(secondScenario.Guid);
        await Assert.That(firstScenario.Steps).Count().IsEqualTo(3);
        await Assert.That(secondScenario.Steps).Count().IsEqualTo(3);
        await Assert.That(Required(firstScenario.Steps)[1].StepText).IsEqualTo("And first action");
        await Assert.That(Required(secondScenario.Steps)[1].StepText).IsEqualTo("And second action");
    }

    [Test]
    public async Task And_when_obeys_run_debug_skipping()
    {
        Options.Debug.ShouldSkipWhenStep = true;
        var calls = new List<string>();
        var scenario = await When(() => calls.Add("when")).And(() => calls.Add("and"))
            .Then(() => calls.Add("then")).BDTestAsync();
        await Assert.That(calls).IsEquivalentTo(new[] { "then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps)[1].Status).IsEqualTo(Status.Skipped);
        await Assert.That(scenario.Steps[1].StepText).StartsWith("[Skipped] And ");

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

        await Assert.That(calls).IsEquivalentTo(new[] { "plain" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(first.Steps)[1].Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(second.Steps)[1].Status).IsEqualTo(Status.Passed);
    }

    [Test]
    public async Task And_when_round_trips_and_renders_in_static_and_hosted_reports()
    {
        using var run = BDTestRun.Start();
        var scenario = await new Capture(run).When(() => NoOp()).And(() => NamedStep("report"))
            .Then(() => NoOp()).BDTestAsync();
        var restored = Required(Required(BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run))).Scenarios).Single();
        await Assert.That(Required(restored.Steps)[1].StepType).IsEqualTo(StepType.AndWhen);
        await Assert.That(restored.Steps[1].StepText).IsEqualTo("And named report");
        await Assert.That(BDTestHtmlHelper.GetTestHtmlReport(null, run)).Contains("<span class=\"kw\">And</span> <span class=\"step-text\">named report</span>");
        await Assert.That(await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", run.Store.ScenariosByInternalId[restored.Guid])).Contains("<span class=\"kw\">And</span> <span class=\"step-text\">named report</span>");
    }

    private static void NoOp() { }

    [StepText("named {value}")]
    private static void NamedStep(string value) { }

    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
