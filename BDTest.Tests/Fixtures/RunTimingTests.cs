using System.Diagnostics;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class RunTimingTests : BDTestBase
{
    private static readonly DateTime StartedAt = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void Setup() => TestResetHelper.ResetData();

    [Test]
    public void Unstarted_scenarios_do_not_change_report_boundaries()
    {
        var later = CreateScenario(StartedAt.AddSeconds(2), StartedAt.AddSeconds(5));
        var unstarted = CreateScenario(default, default);
        var earlier = CreateScenario(StartedAt, StartedAt.AddSeconds(3));

        AssertTimer(BDTestUtil.GetTestTimer(new[] { later, unstarted, earlier }),
            StartedAt, StartedAt.AddSeconds(5));
        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        AssertTimer(report.TestTimer, StartedAt, StartedAt.AddSeconds(5));
    }

    [Test]
    public void All_unstarted_scenarios_have_unknown_boundaries_and_zero_elapsed_time()
    {
        var timer = BDTestUtil.GetTestTimer(new[] { CreateScenario(default, default) });

        AssertTimer(timer, default, default);
    }

    [Test]
    public void Running_scenarios_without_an_end_have_zero_elapsed_time()
    {
        var timer = BDTestUtil.GetTestTimer(new[] { CreateScenario(StartedAt, default) });

        Assert.That(timer.TestsStartedAt, Is.EqualTo(StartedAt));
        Assert.That(timer.TestsFinishedAt, Is.EqualTo(default(DateTime)));
        Assert.That(timer.ElapsedTime, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Empty_selection_preserves_total_report_boundaries()
    {
        var total = new BDTestOutputModel
        {
            TestTimer = new TestTimer { TestsStartedAt = StartedAt, TestsFinishedAt = StartedAt.AddMinutes(1) }
        };

        AssertTimer(BDTestUtil.GetTestTimer(Array.Empty<Scenario>(), total),
            StartedAt, StartedAt.AddMinutes(1));
    }

    [Test]
    public void Empty_run_defaults_use_utc()
    {
        var timer = BDTestUtil.GetTestTimer(Array.Empty<Scenario>());

        Assert.That(timer.TestsStartedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(timer.TestsFinishedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(InternalTestTimeData.ProcessStartTime.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Completed_and_failed_executions_record_utc_and_elapsed_duration(bool fail)
    {
        var measured = new Stopwatch();
        var outer = Stopwatch.StartNew();
        var test = When(() => TimedStep(measured, fail)).Then(() => NoOp());
        Scenario scenario;
        if (fail)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await test.BDTestAsync());
            scenario = BDTestUtil.GetScenarios().Single();
        }
        else
        {
            scenario = await test.BDTestAsync();
        }
        outer.Stop();

        Assert.That(scenario.StartTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(scenario.EndTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(scenario.TimeTaken, Is.InRange(measured.Elapsed, outer.Elapsed));
        var step = scenario.Steps[0];
        Assert.That(step.StartTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(step.EndTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(step.TimeTaken, Is.InRange(measured.Elapsed, scenario.TimeTaken));
        if (fail)
        {
            Assert.That(scenario.Steps[1].StartTime, Is.EqualTo(default(DateTime)));
            Assert.That(scenario.Steps[1].TimeTaken, Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public async Task Scenario_duration_does_not_depend_on_wall_clock_timestamps()
    {
        var outer = Stopwatch.StartNew();
        var scenario = await When(() => MoveStartIntoFuture()).Then(() => NoOp()).BDTestAsync();
        outer.Stop();

        Assert.That(scenario.EndTime, Is.LessThan(scenario.StartTime));
        Assert.That(scenario.TimeTaken, Is.InRange(TimeSpan.Zero, outer.Elapsed));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
        Assert.That(restored.TimeTaken, Is.EqualTo(scenario.TimeTaken));
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Retry_records_the_final_attempt_timestamps_and_duration()
    {
        var attempts = new List<DateTime>();
        var scenario = await When(() => FailFirstAttempt(attempts)).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.StartTime, Is.EqualTo(attempts[1]));
        Assert.That(scenario.StartTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(scenario.TimeTaken, Is.GreaterThanOrEqualTo(scenario.Steps[0].TimeTaken));
        Assert.That(scenario.Steps[0].StartTime, Is.GreaterThanOrEqualTo(scenario.StartTime));
    }

    private Scenario CreateScenario(DateTime start, DateTime end) =>
        new(new List<Step>(), When(() => NoOp())) { StartTime = start, EndTime = end };

    private static void AssertTimer(TestTimer timer, DateTime start, DateTime end)
    {
        Assert.That(timer.TestsStartedAt, Is.EqualTo(start));
        Assert.That(timer.TestsFinishedAt, Is.EqualTo(end));
        Assert.That(timer.ElapsedTime, Is.EqualTo(end - start));
    }

    private static async Task TimedStep(Stopwatch measured, bool fail)
    {
        measured.Start();
        await Task.Delay(20);
        measured.Stop();
        if (fail)
        {
            throw new InvalidOperationException("timed failure");
        }
    }

    private static void MoveStartIntoFuture() =>
        BDTestUtil.GetScenarios().Single().StartTime = DateTime.UtcNow.AddDays(1);

    private static void FailFirstAttempt(List<DateTime> attempts)
    {
        attempts.Add(BDTestUtil.GetScenarios().Single().StartTime);
        if (attempts.Count == 1)
        {
            throw new InvalidOperationException("retry");
        }
    }

    private static void NoOp() { }
}
