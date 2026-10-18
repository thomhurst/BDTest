using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class RetryHookTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RunsAllInstanceHooksIncludingPrivateBaseMethodsAndDerivedAttributes(bool tearDown)
    {
        var fixture = new DerivedHooks();

        await RunHooks(fixture, tearDown);

        Assert.That(fixture.Calls.Take(3), Is.EquivalentTo(new[] { "base-private", "base-protected", "base-public" }));
        Assert.That(fixture.Calls.Skip(3), Is.EquivalentTo(new[] { "derived-private", "derived-public" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InheritsAttributesOnOverridesAndInvokesEachVirtualHookOnlyOnce(bool tearDown)
    {
        var fixture = new OverriddenHooks();

        await RunHooks(fixture, tearDown);

        Assert.That(fixture.Calls, Is.EquivalentTo(new[] { "inherited-override", "attributed-override" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AwaitsBaseHookBeforeStartingDerivedHooks(bool tearDown)
    {
        var fixture = new AsyncDerivedHooks();
        var running = RunHooks(fixture, tearDown);
        try
        {
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "base-start" }));
            Assert.That(running.IsCompleted, Is.False);
        }
        finally
        {
            fixture.Continue.SetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.That(fixture.Calls, Is.EqualTo(new[] { "base-start", "base-end", "derived" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NoHooksIsANoOp(bool tearDown)
    {
        await RunHooks(new NoHooks(), tearDown);
    }

    [TestCase("setup", false, 1)]
    [TestCase("setup", true, 1)]
    [TestCase("teardown", false, 1)]
    [TestCase("teardown", true, 1)]
    [TestCase("before", false, 1)]
    [TestCase("before", true, 1)]
    [TestCase("setup", false, 2)]
    [TestCase("setup", true, 2)]
    [TestCase("teardown", false, 2)]
    [TestCase("teardown", true, 2)]
    [TestCase("before", false, 2)]
    [TestCase("before", true, 2)]
    public async Task RetryFailureRecordsFailedAttemptAndExposesOriginalHookException(
        string hook, bool asynchronous, int failingRetry)
    {
        using var run = BDTestRun.Start();
        using var otherRun = BDTestRun.Start();
        var fixture = new FailingHooks(run, hook, asynchronous, failingRetry);
        try
        {
            var error = await Assert.ThrowsAsync<ErrorOccurredDuringRetryActionException>(() => fixture.RunScenario());

            Assert.That(error, Is.InstanceOf<BDTestException>());
            Assert.That(Required(error).InnerException, Is.SameAs(fixture.Failure));
            Assert.That(Required(error).Message, Does.Contain("retry action").And.Contain(fixture.Failure.Message));
            Assert.That(Required(error).ToString(), Does.Contain(fixture.Failure.ToString()));
            Assert.That(Required(error).GetBaseException(), Is.SameAs(fixture.Failure));
            Assert.That(Required(error).GetBaseException().StackTrace, Does.Contain(nameof(FailingHooks.ThrowHookFailure)));
            Assert.That(fixture.Attempts, Is.EqualTo(failingRetry), "A failing retry hook must stop the retry");

            var scenario = run.Scenarios.Single();
            Assert.That(scenario.Run, Is.SameAs(run));
            Assert.That(scenario.ReportId, Is.EqualTo(run.ReportId));
            Assert.That(otherRun.Scenarios, Is.Empty);
            Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
            Assert.That(scenario.Exception?.Message, Is.EqualTo(fixture.Failure.Message));
            Assert.That(Required(scenario.Exception).StackTrace, Does.Contain(nameof(FailingHooks.ThrowHookFailure)));
            Assert.That(scenario.Output, Is.Not.Null);
            Assert.That(scenario.RetryCount, Is.EqualTo(failingRetry));
            Assert.That(scenario.Attempts, Has.Count.EqualTo(failingRetry + 1));
            for (var index = 0; index < failingRetry; index++)
            {
                var attempt = scenario.Attempts[index];
                Assert.That(attempt.Number, Is.EqualTo(index + 1));
                Assert.That(attempt.Status, Is.EqualTo(Status.Failed));
                Assert.That(attempt.Exception.Message, Is.EqualTo("retry the scenario"));
                Assert.That(attempt.Exception.StackTrace, Does.Contain(nameof(FailingHooks.FailStep)));
            }
            var failedRetry = scenario.Attempts.Last();
            Assert.That(failedRetry.Number, Is.EqualTo(failingRetry + 1));
            Assert.That(failedRetry.Status, Is.EqualTo(Status.Failed));
            Assert.That(failedRetry.Exception.Message, Is.EqualTo(fixture.Failure.Message));
            Assert.That(failedRetry.Exception.StackTrace, Does.Contain(nameof(FailingHooks.ThrowHookFailure)));
            Assert.That(failedRetry.StartTime, Is.GreaterThanOrEqualTo(scenario.Attempts[^2].EndTime));
            Assert.That(failedRetry.EndTime, Is.GreaterThanOrEqualTo(failedRetry.StartTime));
            Assert.That(failedRetry.TimeTaken, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(scenario.StartTime, Is.EqualTo(failedRetry.StartTime));
            Assert.That(scenario.EndTime, Is.EqualTo(failedRetry.EndTime));
            Assert.That(scenario.TimeTaken, Is.EqualTo(failedRetry.TimeTaken));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public void RetryFailureUsesTheStandardInnerExceptionChain()
    {
        var cause = new InvalidOperationException("root cause");
        var hookFailure = new Exception("retry hook failed", cause);

        var error = new ErrorOccurredDuringRetryActionException(hookFailure);

        Assert.That(Required(error).InnerException, Is.SameAs(hookFailure));
        Assert.That(Required(error).GetBaseException(), Is.SameAs(cause));
        Assert.That(Required(error).Message, Does.Contain(hookFailure.Message));
        Assert.That(Required(error).ToString(), Does.Contain(hookFailure.ToString()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PreservesPublicStaticHooks(bool tearDown)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunHooks(new StaticHooks(), tearDown));
        Assert.That(Required(error).Message, Is.EqualTo("static hook ran"));
    }

    private sealed class StaticHooks : BDTestBase
    {
        [BDTestRetrySetUp, BDTestRetryTearDown]
        public static void Hook() => throw new InvalidOperationException("static hook ran");
    }

    private static Task RunHooks(BDTestBase fixture, bool tearDown) => tearDown
        ? fixture.RunMethodWithAttribute<BDTestRetryTearDownAttribute>()
        : fixture.RunMethodWithAttribute<BDTestRetrySetUpAttribute>();

    private sealed class CustomSetUpAttribute : BDTestRetrySetUpAttribute;
    private sealed class CustomTearDownAttribute : BDTestRetryTearDownAttribute;

    private class BaseHooks : BDTestBase
    {
        public List<string> Calls { get; } = [];

        [BDTestRetrySetUp, BDTestRetryTearDown]
        private void PrivateHook() => Calls.Add("base-private");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected void ProtectedHook() => Calls.Add("base-protected");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        public void PublicHook() => Calls.Add("base-public");
    }

    private sealed class DerivedHooks : BaseHooks
    {
        [CustomSetUp, CustomTearDown]
        private void DerivedPrivateHook() => Calls.Add("derived-private");

        [CustomSetUp, CustomTearDown]
        public new void PublicHook() => Calls.Add("derived-public");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        private static void StaticHook() => Assert.Fail("Non-public static methods are not retry hooks");

        public void UnattributedMethod() => Assert.Fail("Only attributed methods are hooks");
    }

    private class VirtualHooks : BDTestBase
    {
        public List<string> Calls { get; } = [];

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected virtual void InheritedHook() => Assert.Fail("Use the override");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected virtual void AttributedHook() => Assert.Fail("Use the override");
    }

    private sealed class OverriddenHooks : VirtualHooks
    {
        protected override void InheritedHook() => Calls.Add("inherited-override");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected override void AttributedHook() => Calls.Add("attributed-override");
    }

    private class AsyncBaseHooks : BDTestBase
    {
        public List<string> Calls { get; } = [];
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [BDTestRetrySetUp, BDTestRetryTearDown]
        private async Task BaseHook()
        {
            Calls.Add("base-start");
            await Continue.Task;
            Calls.Add("base-end");
        }
    }

    private sealed class AsyncDerivedHooks : AsyncBaseHooks
    {
        [BDTestRetrySetUp, BDTestRetryTearDown]
        private void DerivedHook() => Calls.Add("derived");
    }

    private sealed class NoHooks : BDTestBase;

    [BDTestRetry(3)]
    private sealed class FailingHooks(BDTestRun run, string hook, bool asynchronous, int failingRetry) : BDTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;
        public override BDTestRun TestRun => run;
        public InvalidOperationException Failure { get; } = new("retry hook failed");
        public int Attempts { get; private set; }

        public Task<Scenario> RunScenario() => Given(() => FailStep()).When(() => FailStep()).Then(() => FailStep()).BDTestAsync();

        public void FailStep()
        {
            Attempts++;
            throw new Exception("retry the scenario");
        }

        [BDTestRetrySetUp]
        public Task SetUp() => hook == "setup" && Attempts == failingRetry ? ThrowHookFailure() : Task.CompletedTask;

        [BDTestRetryTearDown]
        public Task TearDown() => hook == "teardown" && Attempts == failingRetry ? ThrowHookFailure() : Task.CompletedTask;

        public override Task OnBeforeRetry() => hook == "before" && Attempts == failingRetry
            ? ThrowHookFailure() : Task.CompletedTask;

        public Task ThrowHookFailure()
        {
            if (asynchronous)
            {
                return ThrowHookFailureAsync();
            }

            throw Failure;
        }

        private async Task ThrowHookFailureAsync()
        {
            await Task.Yield();
            throw Failure;
        }

        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
