using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StatusLifecycleTests : TestRunOwner
{
    [Test]
    public async Task NewResultsAndResetStepsAreNotRun()
    {
        var step = new Step(new Runnable(() => NoOp()), StepType.Given, "scenario", "report", TestRun);
        RequireCondition(default(Status) == Status.NotRun, "The default status must represent a test that has not run.");
        await Assert.That(step.Status).IsEqualTo(Status.NotRun);
        await Assert.That(ReportJson.DeserializeObject<Scenario>("{}").Status).IsEqualTo(Status.NotRun);
        step.Status = Status.Failed;
        step.ResetData();
        await Assert.That(step.Status).IsEqualTo(Status.NotRun);
    }

    [Test]
    public async Task RunningScenarioAndStepArePendingAndRetryHooksSeeNotRun()
    {
        using var fixture = new RetryingFixture();
        try
        {
            var scenario = await fixture.Run();
            await Assert.That(fixture.RunningStatuses).IsEquivalentTo(new[]
            {
                (Status.Pending, Status.Pending, Status.NotRun),
                (Status.Pending, Status.Pending, Status.NotRun)
            }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(fixture.ResetStatuses).IsEquivalentTo(new[] { Status.NotRun, Status.NotRun, Status.NotRun }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        }
        finally { fixture.Complete(); }
    }

    [Test]
    [Arguments(Status.NotRun, 5)]
    [Arguments(Status.Pending, 6)]
    [Arguments(Status.Passed, 7)]
    [Arguments(Status.Failed, 1)]
    [Arguments(Status.Inconclusive, 3)]
    [Arguments(Status.NotImplemented, 2)]
    [Arguments(Status.Skipped, 4)]
    [Arguments((Status)12345, int.MaxValue)]
    public async Task SeverityOrderIsStable(Status status, int expected) => await Assert.That(status.GetOrder()).IsEqualTo(expected);

    [Test]
    [Arguments(Status.NotRun)]
    [Arguments(Status.Pending)]
    [Arguments(Status.Inconclusive)]
    public async Task StatusNamesSurviveJsonRoundTrip(Status status)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(status, BDTestJsonContext.Default.Status);
        await Assert.That(json).IsEqualTo($"\"{status}\"");
        await Assert.That(System.Text.Json.JsonSerializer.Deserialize(json, BDTestJsonContext.Default.Status)).IsEqualTo(status);
        var scenario = ReportJson.DeserializeObject<Scenario>($"{{\"Status\":{json},\"Steps\":[{{\"Status\":{json}}}]}}");
        var restored = JObject.Parse(ReportJson.SerializeObject(scenario));
        await Assert.That((string?)restored["Status"]).IsEqualTo(status.ToString());
        await Assert.That((string?)Required(Required(restored["Steps"])[0])["Status"]).IsEqualTo(status.ToString());
    }

    [Test]
    [Arguments(Status.NotRun, "Not run")]
    [Arguments(Status.Pending, "Pending")]
    [Arguments(Status.Inconclusive, "Inconclusive")]
    public async Task HtmlCountsAndLabelsScenarioStatuses(Status status, string label)
    {
        var scenario = ReportJson.DeserializeObject<Scenario>($"{{\"Guid\":\"unfinished\",\"FrameworkTestId\":\"unfinished-test\",\"Status\":\"{status}\",\"Steps\":[{{\"Status\":\"{status}\"}}]}}");
        TestRun.Store.AddScenario(scenario);
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var key = status.ToString().ToLowerInvariant();
        await Assert.That(html).Contains($"<span class=\"label\">{label}</span></span><span class=\"tile-n\">1</span>");
        await Assert.That(html).Contains($"data-filter=\"{key}\" aria-pressed=\"false\"><span>{label}</span><span class=\"n\">1</span>");
        await Assert.That(html).Contains($"class=\"step\" data-status=\"{key}\"");
        await Assert.That(html).Contains($"class=\"step-status\">{label}</span>");
    }

    private static void NoOp() { }

    [BDTestRetry(1)]
    private sealed class RetryingFixture : RunTestBase
    {
        private readonly string _id = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _id;
        public List<(Status, Status, Status)> RunningStatuses { get; } = new();
        public Status[] ResetStatuses { get; private set; } = [];
        public Task<BDTest.Contracts.ScenarioResult> Run() => When(() => FailOnce()).Then(() => NoOp()).BDTestAsync();
        public void FailOnce()
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single();
            RunningStatuses.Add((scenario.Status, Required(scenario.Steps)[0].Status, scenario.Steps[1].Status));
            if (RunningStatuses.Count == 1) throw new InvalidOperationException("Retry probe");
        }
        public override Task OnBeforeRetry()
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single();
            ResetStatuses = new[] { scenario.Status }.Concat(Required(scenario.Steps).Select(step => step.Status)).ToArray();
            return Task.CompletedTask;
        }
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
