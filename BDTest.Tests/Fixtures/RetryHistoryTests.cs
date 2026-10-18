using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class RetryHistoryTests
{
    [SetUp]
    public void Reset() => TestResetHelper.ResetData();

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task KeepsEveryAttemptWhenPassingOrExhaustingRetries(int failures)
    {
        var fixture = new RetryingScenario(failures);
        try
        {
            if (failures > 2)
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Run());
            else
                await fixture.Run();

            var scenario = BDTestUtil.GetScenarios().Single();
            var count = Math.Min(failures + 1, 3);
            Assert.That(scenario.Attempts, Has.Count.EqualTo(count));
            Assert.That(scenario.RetryCount, Is.EqualTo(count - 1));
            Assert.That(scenario.Status, Is.EqualTo(failures > 2 ? Status.Failed : Status.Passed));
            for (var index = 0; index < count; index++)
            {
                var attempt = scenario.Attempts[index];
                Assert.That(attempt.Number, Is.EqualTo(index + 1));
                Assert.That(attempt.Status, Is.EqualTo(index < failures ? Status.Failed : Status.Passed));
                Assert.That(attempt.EndTime, Is.GreaterThanOrEqualTo(attempt.StartTime));
                Assert.That(attempt.TimeTaken, Is.GreaterThan(TimeSpan.Zero));
                if (index < failures)
                {
                    Assert.That(attempt.Exception.Message, Is.EqualTo($"<failure {index + 1}>"));
                    Assert.That(attempt.Exception.StackTrace, Does.Contain(nameof(RetryingScenario.Step)));
                }
                else
                    Assert.That(attempt.Exception, Is.Null);
            }
            Assert.That(scenario.Exception?.Message, Is.EqualTo(failures > 2 ? "<failure 3>" : null));
            Assert.That(scenario.TimeTaken, Is.EqualTo(scenario.Attempts.Last().TimeTaken));
            if (failures == 0)
                Assert.That(BDTestHtmlHelper.GetTestHtmlReport(), Does.Not.Contain("Retry history"));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public async Task HistorySurvivesJsonRoundTripAndAppearsInHtml()
    {
        var fixture = new RetryingScenario(2);
        try
        {
            var scenario = await fixture.Run();
            var restored = JsonConvert.DeserializeObject<Scenario>(JsonConvert.SerializeObject(scenario));
            Assert.That(restored.Attempts.Select(attempt => attempt.Status),
                Is.EqualTo(new[] { Status.Failed, Status.Failed, Status.Passed }));
            Assert.That(restored.Attempts[0].Exception.AsString, Does.Contain("<failure 1>"));
            Assert.That(restored.Attempts[1].TimeTaken, Is.EqualTo(scenario.Attempts[1].TimeTaken));
            Assert.That(restored.Attempts[0].StartTime, Is.EqualTo(scenario.Attempts[0].StartTime));

            var html = BDTestHtmlHelper.GetTestHtmlReport();
            Assert.That(html, Does.Contain("Retry history"));
            Assert.That(html, Does.Contain("Attempt 1: Failed"));
            Assert.That(html, Does.Contain("Attempt 3: Passed"));
            Assert.That(html, Does.Contain("&lt;failure 1&gt;"));
            Assert.That(html, Does.Not.Contain("<failure 1>"));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public void OldReportsHaveEmptyHistory()
    {
        var scenario = JsonConvert.DeserializeObject<Scenario>("{\"RetryCount\":2}");
        Assert.That(scenario.Attempts, Is.Empty);
    }

    [Test]
    public async Task FailingRetryHookDoesNotEraseHistoryOrAddAnUnrunAttempt()
    {
        var fixture = new RetryingScenario(2, failHook: true);
        try
        {
            await Assert.ThrowsAsync<ErrorOccurredDuringRetryActionException>(() => fixture.Run());
            var scenario = BDTestUtil.GetScenarios().Single();
            Assert.That(scenario.RetryCount, Is.EqualTo(1));
            Assert.That(scenario.Attempts, Has.Count.EqualTo(1));
            Assert.That(scenario.Attempts[0].Status, Is.EqualTo(Status.Failed));
            Assert.That(scenario.Attempts[0].Exception.Message, Is.EqualTo("<failure 1>"));

            var html = BDTestHtmlHelper.GetTestHtmlReport();
            Assert.That(html, Does.Contain("Retry history"));
            Assert.That(html, Does.Contain("Attempt 1: Failed"));
            Assert.That(html, Does.Contain("&lt;failure 1&gt;"));
            Assert.That(html, Does.Not.Contain("Attempt 2:"));
            Assert.That(html, Does.Not.Contain("<failure 1>"));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [BDTestRetry(2)]
    private sealed class RetryingScenario(int failures, bool failHook = false) : BDTestBase
    {
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
        public void Complete() => MarkTestAsComplete();
    }
}
