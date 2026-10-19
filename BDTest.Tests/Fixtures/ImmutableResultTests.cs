using System.Text.Json;
using BDTest.Contracts;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class ImmutableResultTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Report_serialization_overloads_preserve_formatting_and_round_trip(bool writeIndented)
    {
        var report = new BDTestReport(BDTestReport.CurrentSchemaVersion, "report", "test",
            new[] { new ScenarioResult("scenario", "report", "story", "scenario", Status.Passed) });
        Func<BDTestReport, string> serializeCompact = BDTestJsonHelper.Serialize;
        var compact = serializeCompact(report);
        var json = BDTestJsonHelper.Serialize(report, writeIndented: writeIndented);

        await Assert.That(compact).IsEqualTo(BDTestJsonHelper.Serialize(report, writeIndented: false));
        await Assert.That(compact.Contains('\n')).IsFalse();
        await Assert.That(json.Contains('\n')).IsEqualTo(writeIndented);
        var restored = BDTestJsonHelper.DeserializeReport(json);
        await Assert.That(restored.Id).IsEqualTo(report.Id);
        await Assert.That(restored.Scenarios.Single().Status).IsEqualTo(Status.Passed);
    }

    [Test]
    [Arguments("scenario")]
    [Arguments("step")]
    [Arguments("query")]
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
            RequireCondition(started.Wait(TimeSpan.FromSeconds(10)), "The background operation did not start.");
            completedWhilePublishing = read.Wait(TimeSpan.FromMilliseconds(100));
            var attachment = new Attachment { Id = "published", Name = "capture.txt", MimeType = "text/plain" };
            scenario.ExecutionAttachments.Add(attachment);
            scenario.ExecutionSteps[0].ExecutionAttachments.Add(attachment);
        }
        var snapshot = await read.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(completedWhilePublishing).IsFalse().Because("A snapshot must wait for an in-flight attachment publication.");
        await Assert.That(snapshot is StepResult step ? step.Attachments.Single().Id
            : ((ScenarioResult)snapshot).Attachments.Single().Id).IsEqualTo("published");
    }

    [Test]
    public async Task Saved_failed_report_renders_the_step_error_once()
    {
        using var run = BDTestRun.Start();
        await Assert.That(async () => await new Capture(run)
            .When(() => throw new InvalidOperationException("saved failure")).Then(() => NoOp())).ThrowsExactly<InvalidOperationException>();
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run));
        var scenario = report.Scenarios.Single();
        await Assert.That(scenario.Exception).IsNotSameReferenceAs(scenario.Steps[0].Exception);
        var html = BDTestHtmlHelper.GenerateHtmlReport(report);
        await Assert.That(html).Contains("saved failure");
        await Assert.That(html).DoesNotContain("<h3 class=\"h3\">Error</h3>");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Saved_report_keeps_distinct_scenario_diagnostics(bool differentInnerError)
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
        await Assert.That(BDTestHtmlHelper.GenerateHtmlReport(restored)).Contains("<h3 class=\"h3\">Error</h3>");
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
        await Assert.That(() => ((IList<string>)result.Tags)[0] = "changed").ThrowsExactly<NotSupportedException>();
        await Assert.That(() => ((IList<StepResult>)result.Steps).Clear()).ThrowsExactly<NotSupportedException>();
        await Assert.That(() => ((IList<ScenarioResult>)BDTestUtil.GetScenarios(run)).Clear()).ThrowsExactly<NotSupportedException>();
        runtime.Tags[0] = "later";
        runtime.TearDownOutput = "teardown";
        runtime.ExecutionSteps[0].Status = Status.Failed;
        var fresh = run.Scenarios.Single();
        await Assert.That(result.Tags).IsEquivalentTo(new[] { "original" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.TearDownOutput).IsNullOrEmpty();
        await Assert.That(result.Steps[0].Status).IsEqualTo(Status.Passed);
        await Assert.That(fresh.Tags).IsEquivalentTo(new[] { "later" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fresh.TearDownOutput).IsEqualTo("teardown");
        BDTestUtil.ClearScenarios(run);
        await Assert.That(result.Steps).Count().IsEqualTo(2);
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
        await Assert.That((before.Select(step => step.Status)).All(value => Equals(value, Status.Pending))).IsTrue();
        await Assert.That((before.Select(step => step.EndTime)).All(value => Equals(value, default(DateTimeOffset)))).IsTrue();
        await Assert.That((after.Select(step => step.Status)).All(value => Equals(value, Status.Passed))).IsTrue();
        await Assert.That((after.Select(step => step.EndTime)).All(value => !Equals(value, default(DateTimeOffset)))).IsTrue();
        await Assert.That(fixture.Before!.Status).IsEqualTo(Status.Pending);
        await Assert.That(fixture.After!.Status).IsEqualTo(Status.Passed);
        await Assert.That((fixture.Before.Steps.Select(step => step.Status)).All(value => Equals(value, Status.NotRun))).IsTrue();
        await Assert.That(result.Attempts).Count().IsEqualTo(1);
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
                .WithDocString(new global::BDTest.StepDocString("text", "text/plain")));
        #pragma warning disable BDT001 // Deliberately retain unexecuted definitions for export.
        _ = new Capture(otherRun).When(() => NoOp()).Then(() => NoOp());
        _ = fixture.When(() => NoOp()).Then(() => NoOp()).WithScenarioText("pending");
        #pragma warning restore BDT001
        var json = BDTestJsonHelper.GetTestJsonData(null, run);
        using var document = JsonDocument.Parse(json);
        await Assert.That(document.RootElement.GetProperty("schemaVersion").GetInt32()).IsEqualTo(BDTestReport.CurrentSchemaVersion);
        await Assert.That(document.RootElement.GetProperty("producerVersion").GetString()).IsNotNull().And.IsNotEmpty();
        var restored = BDTestJsonHelper.DeserializeReport(json);
        await Assert.That(restored.Id).IsEqualTo(run.ReportId);
        await Assert.That(restored.NotRun.Single().ScenarioText).IsEqualTo("pending");
        var row = restored.Scenarios.Single().Examples.Single();
        await Assert.That(row.ExampleValues["Value"]).IsEqualTo("one");
        await Assert.That(row.Attempts.Select(attempt => attempt.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(row.Attempts[0].Steps[0].Exception!.Data["detail"]).IsEqualTo("first attempt");
        await Assert.That(row.Attempts[0].Steps[0].Exception!.TypeName).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(row.Attempts[0].Steps[1].Status).IsEqualTo(Status.NotRun);
        await Assert.That(row.Attempts[1].Steps[1].Arguments.Single()).IsTypeOf<Contracts.StepDocString>();
        await Assert.That(row.Steps[0].StepType).IsEqualTo(StepType.When);
        await Assert.That(outline.EndTime).IsNotEqualTo(default(DateTimeOffset));
        await Assert.That(json).DoesNotContain("\"runnable\"").And.DoesNotContain("\"bdTestBase\"");

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
