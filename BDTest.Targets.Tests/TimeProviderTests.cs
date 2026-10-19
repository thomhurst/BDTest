using Status = BDTest.Contracts.Status;
#if !LEGACY_CORE
using BDTest.Test;
using BDTest.Helpers;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class TimeProviderTests
{
    private static T Required<T>(T? value) where T : class =>
        value ?? throw new AssertionException("Expected a non-null test value.");

    [Test]
    public async Task Run_snapshots_clock_and_records_step_and_attempt_timing()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TimerClock(start);
        var options = new BDTestOptions { TimeProvider = clock };
        using var run = BDTestRun.Start(options);
        options.TimeProvider = TimeProvider.System;
        var scenario = await new TestHost(run).When(() => clock.Fake.Advance(TimeSpan.FromSeconds(3)))
            .Then(() => { }).BDTestAsync();
        Assert.That(scenario.StartTime, Is.EqualTo(start));
        Assert.That(scenario.EndTime, Is.EqualTo(start.AddSeconds(3)));
        Assert.That(scenario.TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(Required(scenario.Steps)[0].TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(Required(scenario.Steps)[0].StartTime, Is.EqualTo(scenario.StartTime));
        Assert.That(Required(scenario.Steps)[0].EndTime, Is.EqualTo(scenario.EndTime));
        Assert.That(Required(scenario.Attempts).Single().TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(3)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Run_clock_drives_timeout_timers(bool scenarioTimeout)
    {
        var clock = new TimerClock();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var step = new TestHost(run).When(async token =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled.TrySetResult(); }
        });
        var configured = scenarioTimeout ? step.WithScenarioTimeout(TimeSpan.FromHours(1)) : step.WithStepTimeout(TimeSpan.FromHours(1));
        var execution = configured.Then(() => { }).BDTestAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await clock.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Fake.Advance(TimeSpan.FromHours(1));
        await Assert.ThrowsAsync<TimeoutException>(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10)));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(run.Scenarios.Single().TimedOut, Is.True);
        Assert.That(run.Scenarios.Single().TimeTaken, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public async Task Elapsed_time_is_monotonic_when_wall_clock_moves_backwards()
    {
        var clock = new TimerClock();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var scenario = await new TestHost(run).When(() =>
        {
            clock.WallOffset = -TimeSpan.FromDays(1);
            clock.Fake.Advance(TimeSpan.FromSeconds(2));
        }).Then(() => { }).BDTestAsync();
        Assert.That(scenario.EndTime, Is.LessThan(scenario.StartTime));
        Assert.That(scenario.TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(Required(scenario.Steps)[0].TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task Retries_record_separate_attempt_durations_from_run_clock()
    {
        var clock = new TimerClock();
        var options = new BDTestOptions { TimeProvider = clock };
        options.RetryTestRules.Add(_ => true, retryLimit: 1);
        using var run = BDTestRun.Start(options);
        var count = 0;
        var scenario = await new TestHost(run).When(() =>
        {
            clock.Fake.Advance(TimeSpan.FromSeconds(++count));
            if (count == 1) throw new InvalidOperationException("retry");
        }).Then(() => { }).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(Required(scenario.Attempts).Select(attempt => attempt.TimeTaken),
            Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }));
    }

    [Test]
    public async Task Outline_records_run_clock_timestamps_and_monotonic_duration()
    {
        var clock = new TimerClock();
        var start = clock.GetUtcNow();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var host = new TestHost(run);
        var outline = await host.ScenarioOutline("Clock outline", new[] { 1, 2 },
            _ => host.When(() =>
            {
                clock.WallOffset = -TimeSpan.FromDays(1);
                clock.Fake.Advance(TimeSpan.FromSeconds(3));
            }).Then(() => { })).BDTestAsync();

        Assert.That(outline.StartTime, Is.EqualTo(start));
        Assert.That(outline.EndTime, Is.EqualTo(start.AddDays(-1).AddSeconds(6)));
        Assert.That(outline.TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(6)));
        Assert.That(Required(outline.Examples).Select(row => row.TimeTaken),
            Is.EqualTo(new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3) }));
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run))
            ?? throw new AssertionException("Expected a report.");
        var timer = report.TestTimer ?? throw new AssertionException("Expected report timing.");
        Assert.That(timer.TestsStartedAt, Is.EqualTo(outline.StartTime));
        Assert.That(timer.TestsFinishedAt, Is.EqualTo(outline.EndTime));
    }

    [Test]
    public async Task Outline_factory_failure_records_run_clock_and_construction_duration()
    {
        var clock = new TimerClock();
        var start = clock.GetUtcNow();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var outlineBuilder = new TestHost(run).ScenarioOutline<int>("Factory failure", new[] { 1 }, _ =>
        {
            clock.WallOffset = -TimeSpan.FromDays(1);
            clock.Fake.Advance(TimeSpan.FromSeconds(4));
            throw new InvalidOperationException("factory failed");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await outlineBuilder.BDTestAsync());

        var outline = run.Scenarios.Single();
        var row = Required(outline.Examples).Single();
        Assert.That(row.StartTime, Is.EqualTo(start));
        Assert.That(row.EndTime, Is.EqualTo(start.AddDays(-1).AddSeconds(4)));
        Assert.That(row.TimeTaken, Is.EqualTo(TimeSpan.FromSeconds(4)));
        Assert.That(outline.StartTime, Is.EqualTo(row.StartTime));
        Assert.That(outline.EndTime, Is.EqualTo(row.EndTime));
        Assert.That(outline.TimeTaken, Is.EqualTo(row.TimeTaken));
        Assert.That(row.Status, Is.EqualTo(Status.Failed));
    }

    [Test]
    public void Empty_report_uses_run_clock_for_both_timestamps()
    {
        var clock = new TimerClock();
        var start = clock.GetUtcNow();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        clock.Fake.Advance(TimeSpan.FromSeconds(5));

        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run))
            ?? throw new AssertionException("Expected a report.");
        var timer = report.TestTimer ?? throw new AssertionException("Expected report timing.");
        Assert.That(timer.TestsStartedAt, Is.EqualTo(start));
        Assert.That(timer.TestsFinishedAt, Is.EqualTo(start.AddSeconds(5)));
        Assert.That(timer.ElapsedTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Null_clock_is_rejected_when_run_starts() =>
        Assert.Throws<ArgumentException>(() => BDTestRun.Start(new BDTestOptions { TimeProvider = null! }));

    private sealed class TestHost(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }

    private sealed class TimerClock(DateTimeOffset? start = null) : TimeProvider
    {
        internal FakeTimeProvider Fake { get; } = new(start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        internal TimeSpan WallOffset { get; set; }
        internal TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() => Fake.GetUtcNow().Add(WallOffset);
        public override long GetTimestamp() => Fake.GetTimestamp();
        public override long TimestampFrequency => Fake.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Fake.CreateTimer(callback, state, dueTime, period);
            TimerCreated.TrySetResult();
            return timer;
        }
    }
}
#endif
