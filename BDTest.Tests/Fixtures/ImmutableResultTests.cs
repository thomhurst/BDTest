using System.Text.Json;
using BDTest.Contracts;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ImmutableResultTests
{
    [TestCase("scenario")]
    [TestCase("step")]
    [TestCase("query")]
    public async Task Snapshot_reads_wait_for_attachment_publication(string boundary)
    {
        using var run = BDTestRun.Start();
        var result = await new Capture(run).When(() => NoOp()).Then(() => NoOp());
        var scenario = run.Store.ScenariosByInternalId[result.Guid];
        using var started = new ManualResetEventSlim();
        Task<object> read;
        bool completedWhilePublishing;
        lock (run.Store.Gate)
        {
            read = Task.Factory.StartNew<object>(() =>
            {
                started.Set();
                return boundary switch
                {
                    "step" => ResultSnapshot.Create(scenario.ExecutionSteps[0]),
                    "query" => run.Scenarios.Single(),
                    _ => ResultSnapshot.Create(scenario)
                };
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(started.Wait(TimeSpan.FromSeconds(10)), Is.True);
            completedWhilePublishing = read.Wait(TimeSpan.FromMilliseconds(100));
            var attachment = new Attachment { Id = "published", Name = "capture.txt", MimeType = "text/plain" };
            scenario.ExecutionAttachments.Add(attachment);
            scenario.ExecutionSteps[0].ExecutionAttachments.Add(attachment);
        }
        var snapshot = await read.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(completedWhilePublishing, Is.False, "A snapshot must wait for an in-flight attachment publication.");
        Assert.That(snapshot is StepResult step ? step.Attachments.Single().Id
            : ((ScenarioResult)snapshot).Attachments.Single().Id, Is.EqualTo("published"));
    }

    [Test]
    public async Task Saved_failed_report_renders_the_step_error_once()
    {
        using var run = BDTestRun.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await new Capture(run)
            .When(() => throw new InvalidOperationException("saved failure")).Then(() => NoOp()));
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run));
        var scenario = report.Scenarios.Single();
        Assert.That(scenario.Exception, Is.Not.SameAs(scenario.Steps[0].Exception));
        var html = BDTestHtmlHelper.GenerateHtmlReport(report);
        Assert.That(html, Does.Contain("saved failure"));
        Assert.That(html, Does.Not.Contain("<h3 class=\"h3\">Error</h3>"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Saved_report_keeps_distinct_scenario_diagnostics(bool differentInnerError)
    {
        var stepError = new ExceptionInfo("Failure", "same message",
            data: new Dictionary<string, string> { ["owner"] = "step" },
            innerExceptions: new[] { new ExceptionInfo("Inner", "step cause") });
        var scenarioError = new ExceptionInfo("Failure", "same message",
            data: new Dictionary<string, string> { ["owner"] = differentInnerError ? "step" : "scenario" },
            innerExceptions: new[] { new ExceptionInfo("Inner", differentInnerError ? "scenario cause" : "step cause") });
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Failed,
            steps: new[] { new StepResult(StepType.When, "step", Status.Failed, exception: stepError) }, exception: scenarioError);
        var report = new BDTestReport(1, "report", "test", new[] { scenario });
        var restored = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.Serialize(report));
        Assert.That(BDTestHtmlHelper.GenerateHtmlReport(restored), Does.Contain("<h3 class=\"h3\">Error</h3>"));
    }

    [Test]
    public async Task Snapshots_cannot_change_execution_and_do_not_change_after_teardown_or_clear()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run);
        var builder = fixture.When(() => NoOp()).Then(() => NoOp());
        builder.Tags = new[] { "original" };
        var result = await builder;
        var runtime = run.Store.ScenariosByInternalId[result.Guid];
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Tags)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<StepResult>)result.Steps).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioResult>)BDTestUtil.GetScenarios(run)).Clear());
        runtime.Tags[0] = "later";
        runtime.TearDownOutput = "teardown";
        runtime.ExecutionSteps[0].Status = Status.Failed;
        var fresh = run.Scenarios.Single();
        Assert.That(result.Tags, Is.EqualTo(new[] { "original" }));
        Assert.That(result.TearDownOutput, Is.Null.Or.Empty);
        Assert.That(result.Steps[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(fresh.Tags, Is.EqualTo(new[] { "later" }));
        Assert.That(fresh.TearDownOutput, Is.EqualTo("teardown"));
        BDTestUtil.ClearScenarios(run);
        Assert.That(result.Steps, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Hooks_receive_fresh_immutable_snapshots_at_each_boundary()
    {
        var before = new List<StepResult>();
        var after = new List<StepResult>();
        var options = new BDTestOptions();
        options.StepHooks.Add(new StepHook(before, after));
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run);
        var result = await fixture.When(() => NoOp()).Then(() => NoOp());
        Assert.That(before.Select(step => step.Status), Is.All.EqualTo(Status.Pending));
        Assert.That(before.Select(step => step.EndTime), Is.All.EqualTo(default(DateTimeOffset)));
        Assert.That(after.Select(step => step.Status), Is.All.EqualTo(Status.Passed));
        Assert.That(after.Select(step => step.EndTime), Is.All.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(fixture.Before!.Status, Is.EqualTo(Status.Pending));
        Assert.That(fixture.After!.Status, Is.EqualTo(Status.Passed));
        Assert.That(fixture.Before.Steps.Select(step => step.Status), Is.All.EqualTo(Status.NotRun));
        Assert.That(result.Attempts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Retry_outline_and_json_preserve_attempt_diagnostics_and_schema_markers()
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(exception => exception is InvalidOperationException, 1);
        using var run = BDTestRun.Start(options);
        using var otherRun = BDTestRun.Start();
        var fixture = new Capture(run);
        var calls = 0;
        var outline = await fixture.ScenarioOutline("row <Value>", new[] { new { Value = "one" } }, row =>
            fixture.When(() => FailOnce()).Then(() => NoOp())
                .WithDocString(new Test.StepDocString("text", "text/plain")));
        #pragma warning disable BDT001 // Deliberately retain unexecuted definitions for export.
        _ = new Capture(otherRun).When(() => NoOp()).Then(() => NoOp());
        _ = fixture.When(() => NoOp()).Then(() => NoOp()).WithScenarioText("pending");
        #pragma warning restore BDT001
        var json = BDTestJsonHelper.GetTestJsonData(null, run);
        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(BDTestReport.CurrentSchemaVersion));
        Assert.That(document.RootElement.GetProperty("producerVersion").GetString(), Is.Not.Null.And.Not.Empty);
        var restored = BDTestJsonHelper.DeserializeReport(json);
        Assert.That(restored.Id, Is.EqualTo(run.ReportId));
        Assert.That(restored.NotRun.Single().ScenarioText, Is.EqualTo("pending"));
        var row = restored.Scenarios.Single().Examples.Single();
        Assert.That(row.ExampleValues["Value"], Is.EqualTo("one"));
        Assert.That(row.Attempts.Select(attempt => attempt.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(row.Attempts[0].Steps[0].Exception!.Data["detail"], Is.EqualTo("first attempt"));
        Assert.That(row.Attempts[0].Steps[0].Exception!.TypeName, Is.EqualTo(typeof(InvalidOperationException).FullName));
        Assert.That(row.Attempts[0].Steps[1].Status, Is.EqualTo(Status.NotRun));
        Assert.That(row.Attempts[1].Steps[1].Arguments.Single(), Is.TypeOf<Contracts.StepDocString>());
        Assert.That(row.Steps[0].StepType, Is.EqualTo(StepType.When));
        Assert.That(outline.EndTime, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(json, Does.Not.Contain("\"runnable\"").And.Not.Contain("\"bdTestBase\""));

        void FailOnce()
        {
            if (++calls == 1)
            {
                var exception = new InvalidOperationException("retry");
                exception.Data["detail"] = "first attempt";
                throw exception;
            }
        }
    }

    private static void NoOp() { }

    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public ScenarioResult? Before { get; private set; }
        public ScenarioResult? After { get; private set; }
        public override Task OnBeforeScenario(ScenarioHookContext context) { Before = context.Scenario; return Task.CompletedTask; }
        public override Task OnAfterScenario(ScenarioHookContext context) { After = context.Scenario; return Task.CompletedTask; }
    }

    private sealed class StepHook(List<StepResult> before, List<StepResult> after) : IStepHook
    {
        public Task BeforeStepAsync(StepResult step) { before.Add(step); return Task.CompletedTask; }
        public Task AfterStepAsync(StepResult step) { after.Add(step); return Task.CompletedTask; }
    }
}
