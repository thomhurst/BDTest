using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StatusLifecycleTests
{
    [SetUp]
    public void Reset() => TestResetHelper.ResetData();

    [Test]
    public void NewResultsAndResetStepsAreNotRun()
    {
        var step = new Step(new Runnable(() => NoOp()), StepType.Given, "scenario", "report");
        Assert.That(default(Status), Is.EqualTo(Status.NotRun));
        Assert.That(step.Status, Is.EqualTo(Status.NotRun));
        Assert.That(ReportJson.DeserializeObject<Scenario>("{}").Status, Is.EqualTo(Status.NotRun));
        step.Status = Status.Failed;
        step.ResetData();
        Assert.That(step.Status, Is.EqualTo(Status.NotRun));
    }

    [Test]
    public async Task RunningScenarioAndStepArePendingAndRetryHooksSeeNotRun()
    {
        var fixture = new RetryingFixture();
        try
        {
            var scenario = await fixture.Run();
            Assert.That(fixture.RunningStatuses, Is.EqualTo(new[]
            {
                (Status.Pending, Status.Pending, Status.NotRun),
                (Status.Pending, Status.Pending, Status.NotRun)
            }));
            Assert.That(fixture.ResetStatuses, Is.EqualTo(new[] { Status.NotRun, Status.NotRun, Status.NotRun }));
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }
        finally { fixture.Complete(); }
    }

    [TestCase(Status.NotRun, 5)]
    [TestCase(Status.Pending, 6)]
    [TestCase(Status.Passed, 7)]
    [TestCase(Status.Failed, 1)]
    [TestCase(Status.Inconclusive, 3)]
    [TestCase(Status.NotImplemented, 2)]
    [TestCase(Status.Skipped, 4)]
    [TestCase((Status)12345, int.MaxValue)]
    public void SeverityOrderIsStable(Status status, int expected) => Assert.That(status.GetOrder(), Is.EqualTo(expected));

    [TestCase(Status.NotRun)]
    [TestCase(Status.Pending)]
    [TestCase(Status.Inconclusive)]
    public void StatusNamesSurviveJsonRoundTrip(Status status)
    {
        var json = ReportJson.SerializeObject(status);
        Assert.That(json, Is.EqualTo($"\"{status}\""));
        Assert.That(ReportJson.DeserializeObject<Status>(json), Is.EqualTo(status));
        var scenario = ReportJson.DeserializeObject<Scenario>($"{{\"Status\":{json},\"Steps\":[{{\"Status\":{json}}}]}}");
        var restored = JObject.Parse(ReportJson.SerializeObject(scenario));
        Assert.That((string?)restored["Status"], Is.EqualTo(status.ToString()));
        Assert.That((string?)Required(Required(restored["Steps"])[0])["Status"], Is.EqualTo(status.ToString()));
    }

    [TestCase(Status.NotRun, "Not run")]
    [TestCase(Status.Pending, "Pending")]
    [TestCase(Status.Inconclusive, "Inconclusive")]
    public void HtmlCountsAndLabelsScenarioStatuses(Status status, string label)
    {
        var scenario = ReportJson.DeserializeObject<Scenario>($"{{\"Guid\":\"unfinished\",\"FrameworkTestId\":\"unfinished-test\",\"Status\":\"{status}\",\"Steps\":[{{\"Status\":\"{status}\"}}]}}");
        BDTest.Maps.TestHolder.AddScenario(scenario);
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var key = status.ToString().ToLowerInvariant();
        Assert.That(html, Does.Contain($"<span class=\"label\">{label}</span></span><span class=\"tile-n\">1</span>"));
        Assert.That(html, Does.Contain($"data-filter=\"{key}\" aria-pressed=\"false\"><span>{label}</span><span class=\"n\">1</span>"));
        Assert.That(html, Does.Contain($"class=\"step\" data-status=\"{key}\""));
        Assert.That(html, Does.Contain($"class=\"step-status\">{label}</span>"));
    }

    private static void NoOp() { }

    [BDTestRetry(1)]
    private sealed class RetryingFixture : BDTestBase
    {
        private readonly string _id = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _id;
        public List<(Status, Status, Status)> RunningStatuses { get; } = new();
        public Status[] ResetStatuses { get; private set; } = [];
        public Task<Scenario> Run() => When(() => FailOnce()).Then(() => NoOp()).BDTestAsync();
        public void FailOnce()
        {
            var scenario = BDTestUtil.GetScenarios().Single();
            RunningStatuses.Add((scenario.Status, Required(scenario.Steps)[0].Status, scenario.Steps[1].Status));
            if (RunningStatuses.Count == 1) throw new InvalidOperationException("Retry probe");
        }
        public override Task OnBeforeRetry()
        {
            var scenario = BDTestUtil.GetScenarios().Single();
            ResetStatuses = new[] { scenario.Status }.Concat(Required(scenario.Steps).Select(step => step.Status)).ToArray();
            return Task.CompletedTask;
        }
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
