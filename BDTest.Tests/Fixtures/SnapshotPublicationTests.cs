using BDTest.Engine;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Contracts;

namespace BDTest.Tests.Fixtures;

public class SnapshotPublicationTests
{
    private sealed class CleanupHook(Func<ScenarioHookContext, Task> after) : BDTest.Interfaces.IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => Task.CompletedTask;
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => after(scenario);
    }
    [Test]
    public async Task Attempt_publication_waits_for_snapshot_readers()
    {
        using var cleanupReached = new ManualResetEventSlim();
        using var finishCleanup = new ManualResetEventSlim();
        var options = new BDTestOptions();
        options.ScenarioHooks.Add(new CleanupHook(_ =>
        {
            cleanupReached.Set();
            if (!finishCleanup.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return Task.CompletedTask;
        }));
        using var run = BDTestRun.Start(options);
        var scenario = new Scenario(new(), new ScenarioDefinition { Run = run, Guid = "scenario", TestId = "test" });
        var execute = Task.Run(() => new ScenarioExecutor(new ScenarioRetryManager()).ExecuteAsync(scenario));
        bool completedWhileReading;
        int countWhileReading;
        try
        {
            await Assert.That(cleanupReached.Wait(TimeSpan.FromSeconds(10))).IsTrue();
            lock (run.Store.Gate)
            {
                finishCleanup.Set();
                completedWhileReading = execute.Wait(TimeSpan.FromMilliseconds(200));
                countWhileReading = scenario.ExecutionAttempts.Count;
            }
        }
        finally { finishCleanup.Set(); }
        await execute.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(completedWhileReading).IsFalse();
        await Assert.That(countWhileReading).IsEqualTo(0);
        await Assert.That(run.Scenarios.Single().Attempts).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Outline_publication_waits_for_snapshot_readers()
    {
        using var run = BDTestRun.Start();
        var definition = new ScenarioDefinition { Run = run, Guid = "outline", TestId = "test" };
        var outline = new Scenario(new(), definition) { IsOutline = true };
        using var started = new ManualResetEventSlim();
        Task<Scenario> publish;
        int countWhileReading;
        lock (run.Store.Gate)
        {
            publish = Task.Factory.StartNew(() =>
            {
                started.Set();
                return new Scenario(new(), definition, outline);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            RequireCondition(started.Wait(TimeSpan.FromSeconds(10)), "The background operation did not start.");
            publish.Wait(TimeSpan.FromMilliseconds(200));
            countWhileReading = outline.ExecutionExamples.Count;
        }
        var example = await publish.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(countWhileReading).IsEqualTo(0);
        await Assert.That(ResultSnapshot.Create(outline).Examples.Single().Guid).IsEqualTo(example.Guid);
    }

    [Test]
    [Arguments("inline")]
    [Arguments("attached")]
    [Arguments("reset")]
    public async Task Diagnostic_publication_waits_for_snapshot_readers(string operation)
    {
        using var run = BDTestRun.Start();
        var step = new Step(new Runnable(() => DiagnosticStep("value")), StepType.When, "scenario", "report", run);
        var readerHeldGate = false;
        object? ReadArgument()
        {
            readerHeldGate = Monitor.IsEntered(run.Store.Gate);
            throw new InvalidOperationException("argument unavailable");
        }
        if (operation == "inline") step.ExplicitArguments = new Func<object?>[] { ReadArgument };
        if (operation == "attached") step.AttachedArguments.Add(() => { ReadArgument(); return new BDTest.Test.StepDocString(""); });
        if (operation == "reset") step.Diagnostics!.Add("previous attempt");
        var before = ResultSnapshot.Create(step);
        using var started = new ManualResetEventSlim();
        Task publish;
        bool completedWhileReading;
        int countWhileReading;
        lock (run.Store.Gate)
        {
            publish = Task.Factory.StartNew(() =>
            {
                started.Set();
                if (operation == "reset") step.ResetData();
                else step.SetStepText(evaluateArguments: true);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            RequireCondition(started.Wait(TimeSpan.FromSeconds(10)), "The background operation did not start.");
            completedWhileReading = publish.Wait(TimeSpan.FromMilliseconds(200));
            countWhileReading = ResultSnapshot.Create(step).Diagnostics.Count;
        }
        await publish.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(completedWhileReading).IsFalse();
        await Assert.That(countWhileReading).IsEqualTo(operation == "reset" ? 1 : 0);
        await Assert.That(ResultSnapshot.Create(step).Diagnostics).Count().IsEqualTo(operation == "reset" ? 0 : 1);
        await Assert.That(before.Diagnostics).Count().IsEqualTo(operation == "reset" ? 1 : 0);
        await Assert.That(readerHeldGate).IsFalse().Because("User argument readers must run outside the snapshot gate.");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Skip_reason_publication_waits_for_snapshot_readers(bool clear)
    {
        using var run = BDTestRun.Start();
        var step = new Step(new Runnable(() => DiagnosticStep("value")), StepType.When, "scenario", "report", run);
        step.SkipReason = new BDTest.Test.StepSkipReason("Rule", "original", 1);
        var before = ResultSnapshot.Create(step);
        using var started = new ManualResetEventSlim();
        Task publish;
        bool completedWhileReading;
        StepResult during;
        lock (run.Store.Gate)
        {
            publish = Task.Factory.StartNew(() =>
            {
                started.Set();
                step.SkipReason = clear ? null : new BDTest.Test.StepSkipReason("DebugSetting", setting: "replacement");
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            RequireCondition(started.Wait(TimeSpan.FromSeconds(10)), "The background operation did not start.");
            completedWhileReading = publish.Wait(TimeSpan.FromMilliseconds(200));
            during = ResultSnapshot.Create(step);
        }
        await publish.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(completedWhileReading).IsFalse();
        await Assert.That(during.SkipReason!.Source).IsEqualTo("Rule");
        await Assert.That(during.SkipReason.AttributeType).IsEqualTo("original");
        await Assert.That(during.SkipReason.RuleIndex).IsEqualTo(1);
        var after = ResultSnapshot.Create(step);
        if (clear) await Assert.That(after.SkipReason).IsNull();
        else await Assert.That(after.SkipReason!.Setting).IsEqualTo("replacement");
        await Assert.That(before.SkipReason!.Source).IsEqualTo("Rule");
    }
    [BDTest.Attributes.StepText("value: {0}")]
    private static void DiagnosticStep(string value) { }

    [Test]
    [Arguments(1)]
    [Arguments(-5)]
    public async Task Saved_report_displays_offset_timestamps_in_utc(int offsetHours)
    {
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(offsetHours));
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Passed,
            startTime: start, endTime: start.AddMinutes(1));
        var report = new BDTestReport(1, "report", "test", new[] { scenario },
            testTimer: new ReportTimer(start, start.AddMinutes(1)));
        var restored = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.Serialize(report));
        var html = BDTestHtmlHelper.GenerateHtmlReport(restored);
        var utc = start.ToUniversalTime();
        await Assert.That(html).Contains($"{utc:yyyy-MM-dd HH:mm:ss} → {utc.AddMinutes(1):HH:mm:ss} UTC");
        await Assert.That(html).Contains($"{utc:yyyy-MM-dd HH:mm:ss.fff} UTC");
        await Assert.That(restored.Scenarios.Single().StartTime.Offset).IsEqualTo(start.Offset);
    }
}
