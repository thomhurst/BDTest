using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ButStepTests : BDTestBase
{
    [TestCase(false)]
    [TestCase(true)]
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

        Assert.That(calls, Is.EqualTo(new[]
        {
            "given", "but given", "second but given", "and given", "but after and given",
            "when", "but when", "second but when", "then", "but then", "second but then",
            "and then", "but after and then"
        }));
        Assert.That(result.Steps.All(step => step.Status == Status.Passed), Is.True);
        Assert.That(result.Steps.Select(step => step.StepText.Split(' ')[0]), Is.EqualTo(new[]
        {
            "Given", "But", "But", "And", "But", "When", "But", "But", "Then", "But", "But", "And", "But"
        }));
        Assert.That(result.Steps[1].StepText, Is.EqualTo("But given override"));
        Assert.That(result.Steps[6].StepText, Is.EqualTo("But when override"));
        Assert.That(result.Steps[9].StepText, Is.EqualTo("But then override"));
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

        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        var restored = report.Scenarios.Single(scenario => scenario.Guid == result.Guid);
        Assert.That(restored.Steps.Select(step => step.StepText), Is.EqualTo(new[]
        {
            "Given no op", "But no op", "When no op", "But named action", "Then no op", "But custom assertion"
        }));
        var json = JObject.Parse(ReportJson.SerializeObject(result));
        Assert.That(json["Steps"].Select(step => (string)step["StepType"]), Is.EqualTo(new[] { "Given", "ButGiven", "When", "ButWhen", "Then", "ButThen" }));
    }

    [Test]
    public async Task But_when_steps_obey_debug_skipping()
    {
        var previous = BDTestSettings.Debug.ShouldSkipWhenStep;
        var calls = new List<string>();
        try
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = true;
            var result = await When(() => Record(calls, "when"))
                .But(() => RecordAsync(calls, "but when"))
                .Then(() => Record(calls, "then"))
                .But(() => Record(calls, "but then"))
                .BDTestAsync();

            Assert.That(calls, Is.EqualTo(new[] { "then", "but then" }));
            Assert.That(result.Steps.Take(2).All(step => step.Status == Status.Skipped), Is.True);
            Assert.That(result.Steps[1].StepText, Does.StartWith("[Skipped] But "));
        }
        finally
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Failing_but_steps_fail_the_scenario_and_stop_later_steps(bool asyncStep)
    {
        var calls = new List<string>();
        var then = When(() => NoOp()).Then(() => NoOp());
        var but = asyncStep ? then.But(() => FailAsync()) : then.But(() => Fail());
        var builder = but.And(() => Record(calls, "must not run"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());
        Assert.That(exception.Message, Is.EqualTo("But failed"));

        var result = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
        Assert.That(result.Status, Is.EqualTo(Status.Failed));
        Assert.That(result.Steps[2].Status, Is.EqualTo(Status.Failed));
        Assert.That(result.Steps[2].StepText, Is.EqualTo(asyncStep ? "But fail async" : "But fail"));
        Assert.That(result.Steps[3].Status, Is.EqualTo(Status.NotRun));
        Assert.That(calls, Is.Empty);
    }

    [TestCase(StepType.Given, 0, "Given")]
    [TestCase(StepType.AndGiven, 1, "And")]
    [TestCase(StepType.When, 2, "When")]
    [TestCase(StepType.Then, 3, "Then")]
    [TestCase(StepType.AndThen, 4, "And")]
    [TestCase(StepType.ButGiven, 5, "But")]
    [TestCase(StepType.ButWhen, 6, "But")]
    [TestCase(StepType.ButThen, 7, "But")]
    [TestCase(StepType.AndWhen, 8, "And")]
    public void Step_types_keep_their_report_values(StepType stepType, int value, string keyword)
    {
        Assert.That((int)stepType, Is.EqualTo(value));
        Assert.That(stepType.GetValue(), Is.EqualTo(keyword));
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
