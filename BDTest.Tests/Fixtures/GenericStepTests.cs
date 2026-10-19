using BDTest.Helpers;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class GenericStepTests : RunTestBase
{
    [Test]
    public async Task Generic_steps_execute_and_round_trip_text_types_and_results()
    {
        var calls = new List<string>();
        var result = await Step(() => Record(calls, "first")).WithArgs(() => calls, () => "first")
            .Step(() => RecordAsync(calls, "second")).WithArgs(() => calls, () => "second")
            .Step(token => RecordWithToken(calls, token)).WithStepText(() => "custom text")
            .WithScenarioText("generic workflow")
            .BDTestAsync();

        await Assert.That(calls).IsEquivalentTo(new[] { "first", "second", "token" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Steps!.All(step => step.Status == Status.Passed)).IsTrue();
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        var restored = report.Scenarios!.Single(scenario => scenario.Guid == result.Guid);
        await Assert.That(restored.Steps!.Select(step => step.StepText)).IsEquivalentTo(new string?[] { "Step record first", "Step record second", "Step custom text" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var html = BDTestHtmlHelper.GenerateHtmlReport(report);
        await Assert.That(html).Contains("<span class=\"kw\">Step</span> <span class=\"step-text\">custom text</span>");
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored);
        await Assert.That(hosted).Contains("<span class=\"kw\">Step</span> <span class=\"step-text\">custom text</span>");
        var json = JObject.Parse(BDTestJsonHelper.GetTestJsonData(null, TestRun))["scenarios"]!.Single();
        await Assert.That(json["steps"]!.All(step => (string?)step["stepType"] == "Step")).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Failure_propagates_and_prevents_later_steps(bool asyncStep)
    {
        var calls = new List<string>();
        var first = asyncStep ? Step(() => FailAsync()) : Step(() => Fail());
        var chain = first.Step(() => Record(calls, "unreached"));
        await Assert.That(async () => await chain.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var result = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == chain.Guid);
        await Assert.That(result.Steps![0].Status).IsEqualTo(Status.Failed);
        await Assert.That(result.Steps[1].Status).IsEqualTo(Status.NotRun);
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    public async Task Single_and_value_task_steps_are_runnable()
    {
        var single = await Step(() => NoOp());
        await Assert.That(single.Steps!.Count).IsEqualTo(1);
        var result = await Step(() => ValueAsync())
            .Step(token => ValueWithTokenAsync(token))
            .Step(() => ValueResultAsync())
            .Step(token => ValueResultWithTokenAsync(token));
        await Assert.That(result.Steps!.Count).IsEqualTo(4);
        await Assert.That(result.Steps.All(step => step.Status == Status.Passed)).IsTrue();
    }

    [Test]
    public async Task Cancellation_reaches_generic_token_steps()
    {
        using var cancellation = new CancellationTokenSource();
        var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chain = Step(async token =>
        {
            cancellation.Cancel();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { observed.TrySetResult(token.IsCancellationRequested); }
        });
        await Assert.That(async () => await chain.BDTestAsync(cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(await observed.Task.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
    }

    [StepText("record {value}")]
    private static void Record(List<string> calls, string value) => calls.Add(value);
    [StepText("record {value}")]
    private static async Task RecordAsync(List<string> calls, string value) { await Task.Yield(); calls.Add(value); }
    private static Task RecordWithToken(List<string> calls, CancellationToken token) { token.ThrowIfCancellationRequested(); calls.Add("token"); return Task.CompletedTask; }
    private static void Fail() => throw new InvalidOperationException("generic failure");
    private static async Task FailAsync() { await Task.Yield(); Fail(); }
    private static void NoOp() { }
    private static ValueTask ValueAsync() => default;
    private static ValueTask ValueWithTokenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return default; }
    private static ValueTask<int> ValueResultAsync() => new(42);
    private static ValueTask<int> ValueResultWithTokenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return new(42); }
}
