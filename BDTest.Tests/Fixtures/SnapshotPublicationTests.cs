using BDTest.Engine;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Contracts;
using NUnit.Framework;

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
            Assert.That(cleanupReached.Wait(TimeSpan.FromSeconds(10)), Is.True);
            lock (run.Store.Gate)
            {
                finishCleanup.Set();
                completedWhileReading = execute.Wait(TimeSpan.FromMilliseconds(200));
                countWhileReading = scenario.ExecutionAttempts.Count;
            }
        }
        finally { finishCleanup.Set(); }
        await execute.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(completedWhileReading, Is.False);
        Assert.That(countWhileReading, Is.Zero);
        Assert.That(run.Scenarios.Single().Attempts, Has.Count.EqualTo(1));
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
            Assert.That(started.Wait(TimeSpan.FromSeconds(10)), Is.True);
            publish.Wait(TimeSpan.FromMilliseconds(200));
            countWhileReading = outline.ExecutionExamples.Count;
        }
        var example = await publish.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(countWhileReading, Is.Zero);
        Assert.That(ResultSnapshot.Create(outline).Examples.Single().Guid, Is.EqualTo(example.Guid));
    }

    [TestCase(1)]
    [TestCase(-5)]
    public void Saved_report_displays_offset_timestamps_in_utc(int offsetHours)
    {
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(offsetHours));
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Passed,
            startTime: start, endTime: start.AddMinutes(1));
        var report = new BDTestReport(1, "report", "test", new[] { scenario },
            testTimer: new ReportTimer(start, start.AddMinutes(1)));
        var restored = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.Serialize(report));
        var html = BDTestHtmlHelper.GenerateHtmlReport(restored);
        var utc = start.ToUniversalTime();
        Assert.That(html, Does.Contain($"{utc:yyyy-MM-dd HH:mm:ss} → {utc.AddMinutes(1):HH:mm:ss} UTC"));
        Assert.That(html, Does.Contain($"{utc:yyyy-MM-dd HH:mm:ss.fff} UTC"));
        Assert.That(restored.Scenarios.Single().StartTime.Offset, Is.EqualTo(start.Offset));
    }
}

