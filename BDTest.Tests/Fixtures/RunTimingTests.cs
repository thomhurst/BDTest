using System.Diagnostics;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class RunTimingTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    private static readonly DateTime StartedAt = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Unstarted_scenarios_do_not_change_report_boundaries()
    {
        var later = CreateScenario(StartedAt.AddSeconds(2), StartedAt.AddSeconds(5));
        var unstarted = CreateScenario(default, default);
        var earlier = CreateScenario(StartedAt, StartedAt.AddSeconds(3));

        await AssertTimer(BDTestUtil.GetTestTimer(new[] { later, unstarted, earlier }),
            StartedAt, StartedAt.AddSeconds(5));
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        await Assert.That(report.TestTimer!.TestsStartedAt).IsEqualTo(new DateTimeOffset(StartedAt));
        await Assert.That(report.TestTimer.TestsFinishedAt).IsEqualTo(new DateTimeOffset(StartedAt.AddSeconds(5)));
    }

    [Test]
    public async Task All_unstarted_scenarios_have_unknown_boundaries_and_zero_elapsed_time()
    {
        var timer = BDTestUtil.GetTestTimer(new[] { CreateScenario(default, default) });

        await AssertTimer(timer, default, default);
    }

    [Test]
    public async Task Running_scenarios_without_an_end_have_zero_elapsed_time()
    {
        var timer = BDTestUtil.GetTestTimer(new[] { CreateScenario(StartedAt, default) });

        await Assert.That(timer.TestsStartedAt).IsEqualTo(StartedAt);
        await Assert.That(timer.TestsFinishedAt).IsEqualTo(default(DateTime));
        await Assert.That(timer.ElapsedTime).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task Empty_selection_preserves_total_report_boundaries()
    {
        var total = new BDTestOutputModel
        {
            TestTimer = new TestTimer { TestsStartedAt = StartedAt, TestsFinishedAt = StartedAt.AddMinutes(1) }
        };

        await AssertTimer(BDTestUtil.GetTestTimer(Array.Empty<Scenario>(), total),
            StartedAt, StartedAt.AddMinutes(1));
    }

    [Test]
    public async Task Empty_run_defaults_use_utc()
    {
        var timer = Required(BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, TestRun))).TestTimer!;

        await Assert.That(timer.TestsStartedAt.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(timer.TestsFinishedAt.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(InternalTestTimeData.ProcessStartTime.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Completed_and_failed_executions_record_utc_and_elapsed_duration(bool fail)
    {
        var measured = new Stopwatch();
        var outer = Stopwatch.StartNew();
        var test = When(() => TimedStep(measured, fail)).Then(() => NoOp());
        BDTest.Contracts.ScenarioResult scenario;
        if (fail)
        {
            await Assert.That(async () => await test.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
            scenario = BDTestUtil.GetScenarios(TestRun).Single();
        }
        else
        {
            scenario = await test.BDTestAsync();
        }
        outer.Stop();

        await Assert.That(scenario.StartTime.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(scenario.EndTime.Offset).IsEqualTo(TimeSpan.Zero);

        await Assert.That(scenario.TimeTaken).IsGreaterThanOrEqualTo(measured.Elapsed).And.IsLessThanOrEqualTo(outer.Elapsed);
        var step = Required(scenario.Steps)[0];
        await Assert.That(step.StartTime.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(step.EndTime.Offset).IsEqualTo(TimeSpan.Zero);

        await Assert.That(step.TimeTaken).IsGreaterThanOrEqualTo(measured.Elapsed).And.IsLessThanOrEqualTo(scenario.TimeTaken);
        if (fail)
        {
            await Assert.That(scenario.Steps[1].StartTime).IsEqualTo(default(DateTimeOffset));
            await Assert.That(scenario.Steps[1].TimeTaken).IsEqualTo(TimeSpan.Zero);
        }
    }

    [Test]
    public async Task Scenario_duration_does_not_depend_on_wall_clock_timestamps()
    {
        var outer = Stopwatch.StartNew();
        var scenario = await When(() => MoveStartIntoFuture()).Then(() => NoOp()).BDTestAsync();
        outer.Stop();

        await Assert.That(scenario.EndTime).IsLessThan(scenario.StartTime);

        await Assert.That(scenario.TimeTaken).IsGreaterThanOrEqualTo(TimeSpan.Zero).And.IsLessThanOrEqualTo(outer.Elapsed);
        var restored = ReportJson.DeserializeObject<BDTest.Contracts.ScenarioResult>(ReportJson.SerializeObject(scenario));
        await Assert.That(restored.TimeTaken).IsEqualTo(scenario.TimeTaken);
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Retry_records_the_final_attempt_timestamps_and_duration()
    {
        var attempts = new List<DateTime>();
        var scenario = await When(() => FailFirstAttempt(attempts)).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.RetryCount).IsEqualTo(1);
        await Assert.That(scenario.StartTime).IsEqualTo(new DateTimeOffset(attempts[1]));
        await Assert.That(scenario.StartTime.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(scenario.TimeTaken).IsGreaterThanOrEqualTo(Required(scenario.Steps)[0].TimeTaken);
        await Assert.That(scenario.Steps[0].StartTime).IsGreaterThanOrEqualTo(scenario.StartTime);
    }

    private Scenario CreateScenario(DateTime start, DateTime end) =>
        new(new List<Step>(), When(() => NoOp()).Definition) { StartTime = start, EndTime = end };

    private static async Task AssertTimer(TestTimer timer, DateTime start, DateTime end)
    {
        await Assert.That(timer.TestsStartedAt).IsEqualTo(start);
        await Assert.That(timer.TestsFinishedAt).IsEqualTo(end);
        await Assert.That(timer.ElapsedTime).IsEqualTo(end - start);
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

    private void MoveStartIntoFuture() =>
        TestRun.Store.ScenariosByInternalId.Values.Single().StartTime = DateTime.UtcNow.AddDays(1);

    private void FailFirstAttempt(List<DateTime> attempts)
    {
        attempts.Add(TestRun.Store.ScenariosByInternalId.Values.Single().StartTime);
        if (attempts.Count == 1)
        {
            throw new InvalidOperationException("retry");
        }
    }

    private static void NoOp() { }
}
