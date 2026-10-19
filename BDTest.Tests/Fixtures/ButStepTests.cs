using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ButStepTests : RunTestBase
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task But_steps_preserve_their_phase_and_execute_in_order(bool asyncSteps)
    {
        var calls = new List<string>();
        var given = Given(() => Record(calls, "given"));
        var result = asyncSteps
            ? await given.But(() => RecordAsync(calls, "but given")).WithStepText(() => "given override")
                .But(() => RecordAsync(calls, "second but given"))
                .And(() => RecordAsync(calls, "and given"))
                .But(() => RecordAsync(calls, "but after and given"))
                .When(() => RecordAsync(calls, "when"))
                .But(() => RecordAsync(calls, "but when")).WithStepText(() => "when override")
                .But(() => RecordAsync(calls, "second but when"))
                .Then(() => RecordAsync(calls, "then"))
                .But(() => RecordAsync(calls, "but then")).WithStepText(() => "then override")
                .But(() => RecordAsync(calls, "second but then"))
                .And(() => RecordAsync(calls, "and then"))
                .But(() => RecordAsync(calls, "but after and then"))
                .BDTestAsync()
            : await given.But(() => Record(calls, "but given")).WithStepText(() => "given override")
                .But(() => Record(calls, "second but given"))
                .And(() => Record(calls, "and given"))
                .But(() => Record(calls, "but after and given"))
                .When(() => Record(calls, "when"))
                .But(() => Record(calls, "but when")).WithStepText(() => "when override")
                .But(() => Record(calls, "second but when"))
                .Then(() => Record(calls, "then"))
                .But(() => Record(calls, "but then")).WithStepText(() => "then override")
                .But(() => Record(calls, "second but then"))
                .And(() => Record(calls, "and then"))
                .But(() => Record(calls, "but after and then"))
                .BDTestAsync();

        await Assert.That(calls).IsEquivalentTo(new[]
        {
            "given", "but given", "second but given", "and given", "but after and given",
            "when", "but when", "second but when", "then", "but then", "second but then",
            "and then", "but after and then"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(result.Steps).All(step => step.Status == Status.Passed)).IsTrue();
        await Assert.That(result.Steps.Select(step => Required(step.StepText).Split(' ')[0])).IsEquivalentTo(new[]
        {
            "Given", "But", "But", "And", "But", "When", "But", "But", "Then", "But", "But", "And", "But"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Steps[1].StepText).IsEqualTo("But given override");
        await Assert.That(result.Steps[6].StepText).IsEqualTo("But when override");
        await Assert.That(result.Steps[9].StepText).IsEqualTo("But then override");
    }

    [Test]
    public async Task But_steps_keep_report_text_and_serialize_their_types()
    {
        var result = await Given(() => NoOp())
            .But(() => NoOp())
            .When(() => NoOp())
            .But(() => NamedStepAsync("action"))
            .Then(() => NoOp())
            .But(() => NoOp()).WithStepText(() => "custom assertion")
            .BDTestAsync();

        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        var restored = Required(report.Scenarios).Single(scenario => scenario.Guid == result.Guid);
        await Assert.That(Required(restored.Steps).Select(step => step.StepText)).IsEquivalentTo((new[]
        {
            "Given no op", "But no op", "When no op", "But named action", "Then no op", "But custom assertion"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var json = JObject.Parse(ReportJson.SerializeObject(result));
        await Assert.That(Required(json["Steps"]).Select(step => (string?)step["StepType"])).IsEquivalentTo((new[] { "Given", "ButGiven", "When", "ButWhen", "Then", "ButThen" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task But_when_steps_obey_debug_skipping()
    {
        var calls = new List<string>();
        Options.Debug.ShouldSkipWhenStep = true;
        var result = await When(() => Record(calls, "when"))
            .But(() => RecordAsync(calls, "but when"))
            .Then(() => Record(calls, "then"))
            .But(() => Record(calls, "but then"))
            .BDTestAsync();

        await Assert.That(calls).IsEquivalentTo(new[] { "then", "but then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(result.Steps).Take(2).All(step => step.Status == Status.Skipped)).IsTrue();
        await Assert.That(result.Steps[1].StepText).StartsWith("[Skipped] But ");

    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Failing_but_steps_fail_the_scenario_and_stop_later_steps(bool asyncStep)
    {
        var calls = new List<string>();
        var then = When(() => NoOp()).Then(() => NoOp());
        var but = asyncStep ? then.But(() => FailAsync()) : then.But(() => Fail());
        var builder = but.And(() => Record(calls, "must not run"));

        var exception = await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(Required(exception).Message).IsEqualTo("But failed");

        var result = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
        await Assert.That(result.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(result.Steps)[2].Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(result.Steps)[2].StepText).IsEqualTo(asyncStep ? "But fail async" : "But fail");
        await Assert.That(Required(result.Steps)[3].Status).IsEqualTo(Status.NotRun);
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    [Arguments(StepType.Given, 0, "Given")]
    [Arguments(StepType.AndGiven, 1, "And")]
    [Arguments(StepType.When, 2, "When")]
    [Arguments(StepType.Then, 3, "Then")]
    [Arguments(StepType.AndThen, 4, "And")]
    [Arguments(StepType.ButGiven, 5, "But")]
    [Arguments(StepType.ButWhen, 6, "But")]
    [Arguments(StepType.ButThen, 7, "But")]
    [Arguments(StepType.AndWhen, 8, "And")]
    public async Task Step_types_keep_their_report_values(StepType stepType, int value, string keyword)
    {
        await Assert.That((int)stepType).IsEqualTo(value);
        await Assert.That(stepType.GetValue()).IsEqualTo(keyword);
    }

    private static void Record(List<string> calls, string value) => calls.Add(value);

    private static async Task RecordAsync(List<string> calls, string value)
    {
        await Task.Yield();
        calls.Add(value);
    }

    private static void NoOp() { }

    [StepText("named {0}")]
    private static Task NamedStepAsync(string value) => Task.CompletedTask;

    private static void Fail() => throw new InvalidOperationException("But failed");

    private static async Task FailAsync()
    {
        await Task.Yield();
        Fail();
    }
}
