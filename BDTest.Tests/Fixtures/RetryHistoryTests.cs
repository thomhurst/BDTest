using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class RetryHistoryTests : TestRunOwner
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task KeepsEveryAttemptWhenPassingOrExhaustingRetries(int failures)
    {
        var fixture = new RetryingScenario(TestRun, failures);
        try
        {
            if (failures > 2)
                await Assert.That(async () => { await fixture.Run(); }).ThrowsExactly<InvalidOperationException>();
            else
                await fixture.Run();

            var scenario = BDTestUtil.GetScenarios(TestRun).Single();
            var count = Math.Min(failures + 1, 3);
            await Assert.That(scenario.Attempts).Count().IsEqualTo(count);
            await Assert.That(scenario.RetryCount).IsEqualTo(count - 1);
            await Assert.That(scenario.Status).IsEqualTo(failures > 2 ? Status.Failed : Status.Passed);
            for (var index = 0; index < count; index++)
            {
                var attempt = Required(scenario.Attempts)[index];
                await Assert.That(attempt.Number).IsEqualTo(index + 1);
                await Assert.That(attempt.Status).IsEqualTo(index < failures ? Status.Failed : Status.Passed);
                await Assert.That(attempt.EndTime).IsGreaterThanOrEqualTo(attempt.StartTime);
                await Assert.That(attempt.TimeTaken).IsGreaterThan(TimeSpan.Zero);
                if (index < failures)
                {
                    await Assert.That(Required(attempt.Exception).Message).IsEqualTo($"<failure {index + 1}>");
                    await Assert.That(Required(attempt.Exception).StackTrace).Contains(nameof(RetryingScenario.Step));
                }
                else
                    await Assert.That(attempt.Exception).IsNull();
            }
            await Assert.That(scenario.Exception?.Message).IsEqualTo(failures > 2 ? "<failure 3>" : null);
            await Assert.That(scenario.TimeTaken).IsEqualTo(Required(scenario.Attempts).Last().TimeTaken);
            if (failures == 0)
                await Assert.That(BDTestHtmlHelper.GetTestHtmlReport(null, TestRun)).DoesNotContain("Retry history");
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public async Task HistorySurvivesJsonRoundTripAndAppearsInHtml()
    {
        var fixture = new RetryingScenario(TestRun, 2);
        try
        {
            var scenario = await fixture.Run();
            var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
            await Assert.That(Required(restored.Attempts).Select(attempt => attempt.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(Required(restored.Attempts[0].Exception).AsString).Contains("<failure 1>");
            await Assert.That(restored.Attempts[1].TimeTaken).IsEqualTo(Required(scenario.Attempts)[1].TimeTaken);
            await Assert.That(restored.Attempts[0].StartTime).IsEqualTo(scenario.Attempts[0].StartTime);

            var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
            await Assert.That(html).Contains("Retry history");
            await Assert.That(html).Contains("Attempt 1 · Failed");
            await Assert.That(html).Contains("Attempt 3 · Passed");
            await Assert.That(html).Contains("&lt;failure 1&gt;");
            await Assert.That(html).DoesNotContain("<failure 1>");
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public async Task OldReportsHaveEmptyHistory()
    {
        var scenario = ReportJson.DeserializeObject<Scenario>("{\"RetryCount\":2}");
        await Assert.That(scenario.Attempts).IsEmpty();
    }

    [Test]
    public async Task FailingRetryHookKeepsBothStepAndHookFailuresInHistory()
    {
        var fixture = new RetryingScenario(TestRun, 2, failHook: true);
        try
        {
            await Assert.That(async () => { await fixture.Run(); }).ThrowsExactly<ErrorOccurredDuringRetryActionException>();
            var scenario = BDTestUtil.GetScenarios(TestRun).Single();
            await Assert.That(scenario.RetryCount).IsEqualTo(1);
            await Assert.That(scenario.Attempts).Count().IsEqualTo(2);
            await Assert.That(Required(scenario.Attempts)[1].Status).IsEqualTo(Status.Failed);
            await Assert.That(Required(scenario.Attempts[1].Exception).Message).IsEqualTo("hook failed");
            await Assert.That(scenario.Attempts[0].Status).IsEqualTo(Status.Failed);
            await Assert.That(Required(scenario.Attempts[0].Exception).Message).IsEqualTo("<failure 1>");

            var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
            await Assert.That(html).Contains("Retry history");
            await Assert.That(html).Contains("Attempt 1 · Failed");
            await Assert.That(html).Contains("&lt;failure 1&gt;");
            await Assert.That(html).Contains("Attempt 2 · Failed");
            await Assert.That(html).Contains("hook failed");
            var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
            await Assert.That(Required(restored.Exception).Message).IsEqualTo("hook failed");
            await Assert.That(Required(restored.Attempts).Select(attempt => Required(attempt.Exception).Message)).IsEquivalentTo((new[] { "<failure 1>", "hook failed" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(html).DoesNotContain("<failure 1>");
        }
        finally
        {
            fixture.Complete();
        }
    }

    [BDTestRetry(2)]
    private sealed class RetryingScenario(BDTestRun run, int failures, bool failHook = false) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        private readonly string _executionId = Guid.NewGuid().ToString();
        private int _runs;
        protected override string BDTestExecutionId => _executionId;
        public Task<Scenario> Run() => When(() => Step()).Then(() => Done()).BDTestAsync();
        public void Step()
        {
            if (++_runs <= failures)
                throw new InvalidOperationException($"<failure {_runs}>");
        }
        private static void Done() { }
        public override Task OnBeforeRetry() => failHook
            ? throw new InvalidOperationException("hook failed")
            : Task.CompletedTask;
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
