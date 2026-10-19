using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class RetryHookTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunsAllInstanceHooksIncludingPrivateBaseMethodsAndDerivedAttributes(bool tearDown)
    {
        using var fixture = new DerivedHooks();

        await RunHooks(fixture, tearDown);


        await Assert.That(fixture.Calls.Take(3)).IsEquivalentTo(new[] { "base-private", "base-protected", "base-public" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        await Assert.That(fixture.Calls.Skip(3)).IsEquivalentTo(new[] { "derived-private", "derived-public" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InheritsAttributesOnOverridesAndInvokesEachVirtualHookOnlyOnce(bool tearDown)
    {
        using var fixture = new OverriddenHooks();

        await RunHooks(fixture, tearDown);


        await Assert.That(fixture.Calls).IsEquivalentTo(new[] { "inherited-override", "attributed-override" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AwaitsBaseHookBeforeStartingDerivedHooks(bool tearDown)
    {
        using var fixture = new AsyncDerivedHooks();
        var running = RunHooks(fixture, tearDown);
        try
        {
            await Assert.That(fixture.Calls).IsEquivalentTo(new[] { "base-start" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(running.IsCompleted).IsFalse();
        }
        finally
        {
            fixture.Continue.SetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await Assert.That(fixture.Calls).IsEquivalentTo(new[] { "base-start", "base-end", "derived" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NoHooksIsANoOp(bool tearDown)
    {
        using var fixture = new NoHooks();
        await RunHooks(fixture, tearDown);
    }

    [Test]
    [Arguments("setup", false, 1)]
    [Arguments("setup", true, 1)]
    [Arguments("teardown", false, 1)]
    [Arguments("teardown", true, 1)]
    [Arguments("before", false, 1)]
    [Arguments("before", true, 1)]
    [Arguments("setup", false, 2)]
    [Arguments("setup", true, 2)]
    [Arguments("teardown", false, 2)]
    [Arguments("teardown", true, 2)]
    [Arguments("before", false, 2)]
    [Arguments("before", true, 2)]
    public async Task RetryFailureRecordsFailedAttemptAndExposesOriginalHookException(
        string hook, bool asynchronous, int failingRetry)
    {
        using var run = BDTestRun.Start();
        using var otherRun = BDTestRun.Start();
        var fixture = new FailingHooks(run, hook, asynchronous, failingRetry);
        try
        {
            var error = await Assert.That(async () => { await fixture.RunScenario(); }).ThrowsExactly<ErrorOccurredDuringRetryActionException>();

            await Assert.That(error).IsAssignableTo<BDTestException>();
            await Assert.That(Required(error).InnerException).IsSameReferenceAs(fixture.Failure);

            await Assert.That(Required(error).Message).Contains("retry action").And.Contains(fixture.Failure.Message);
            await Assert.That(Required(error).ToString()).Contains(fixture.Failure.ToString());
            await Assert.That(Required(error).GetBaseException()).IsSameReferenceAs(fixture.Failure);
            await Assert.That(Required(error).GetBaseException().StackTrace).Contains(nameof(FailingHooks.ThrowHookFailure));
            await Assert.That(fixture.Attempts).IsEqualTo(failingRetry).Because("A failing retry hook must stop the retry");

            var scenario = run.Scenarios.Single();
            await Assert.That(scenario.Run).IsSameReferenceAs(run);
            await Assert.That(scenario.ReportId).IsEqualTo(run.ReportId);
            await Assert.That(otherRun.Scenarios).IsEmpty();
            await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
            await Assert.That(scenario.Exception?.Message).IsEqualTo(fixture.Failure.Message);
            await Assert.That(Required(scenario.Exception).StackTrace).Contains(nameof(FailingHooks.ThrowHookFailure));
            await Assert.That(scenario.Output).IsNotNull();
            await Assert.That(scenario.RetryCount).IsEqualTo(failingRetry);
            await Assert.That(scenario.Attempts).Count().IsEqualTo(failingRetry + 1);
            for (var index = 0; index < failingRetry; index++)
            {
                var attempt = Required(scenario.Attempts)[index];
                await Assert.That(attempt.Number).IsEqualTo(index + 1);
                await Assert.That(attempt.Status).IsEqualTo(Status.Failed);
                await Assert.That(Required(attempt.Exception).Message).IsEqualTo("retry the scenario");
                await Assert.That(Required(attempt.Exception).StackTrace).Contains(nameof(FailingHooks.FailStep));
            }
            var failedRetry = Required(scenario.Attempts).Last();
            await Assert.That(failedRetry.Number).IsEqualTo(failingRetry + 1);
            await Assert.That(failedRetry.Status).IsEqualTo(Status.Failed);
            await Assert.That(Required(failedRetry.Exception).Message).IsEqualTo(fixture.Failure.Message);
            await Assert.That(Required(failedRetry.Exception).StackTrace).Contains(nameof(FailingHooks.ThrowHookFailure));
            await Assert.That(failedRetry.StartTime).IsGreaterThanOrEqualTo(scenario.Attempts[^2].EndTime);
            await Assert.That(failedRetry.EndTime).IsGreaterThanOrEqualTo(failedRetry.StartTime);
            await Assert.That(failedRetry.TimeTaken).IsGreaterThan(TimeSpan.Zero);
            await Assert.That(scenario.StartTime).IsEqualTo(failedRetry.StartTime);
            await Assert.That(scenario.EndTime).IsEqualTo(failedRetry.EndTime);
            await Assert.That(scenario.TimeTaken).IsEqualTo(failedRetry.TimeTaken);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    public async Task RetryFailureUsesTheStandardInnerExceptionChain()
    {
        var cause = new InvalidOperationException("root cause");
        var hookFailure = new Exception("retry hook failed", cause);

        var error = new ErrorOccurredDuringRetryActionException(hookFailure);

        await Assert.That(Required(error).InnerException).IsSameReferenceAs(hookFailure);
        await Assert.That(Required(error).GetBaseException()).IsSameReferenceAs(cause);
        await Assert.That(Required(error).Message).Contains(hookFailure.Message);
        await Assert.That(Required(error).ToString()).Contains(hookFailure.ToString());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PreservesPublicStaticHooks(bool tearDown)
    {
        using var fixture = new StaticHooks();
        var error = await Assert.That(() => RunHooks(fixture, tearDown)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(Required(error).Message).IsEqualTo("static hook ran");
    }

    private sealed class StaticHooks : RunTestBase
    {
        [BDTestRetrySetUp, BDTestRetryTearDown]
        public static void Hook() => throw new InvalidOperationException("static hook ran");
    }

    private static Task RunHooks(BDTestBase fixture, bool tearDown) => tearDown
        ? fixture.RunMethodWithAttribute<BDTestRetryTearDownAttribute>()
        : fixture.RunMethodWithAttribute<BDTestRetrySetUpAttribute>();

    private sealed class CustomSetUpAttribute : BDTestRetrySetUpAttribute;
    private sealed class CustomTearDownAttribute : BDTestRetryTearDownAttribute;

    private class BaseHooks : RunTestBase
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
        private static void StaticHook() => Fail.Test("Non-public static methods are not retry hooks");

        public void UnattributedMethod() => Fail.Test("Only attributed methods are hooks");
    }

    private class VirtualHooks : RunTestBase
    {
        public List<string> Calls { get; } = [];

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected virtual void InheritedHook() => Fail.Test("Use the override");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected virtual void AttributedHook() => Fail.Test("Use the override");
    }

    private sealed class OverriddenHooks : VirtualHooks
    {
        protected override void InheritedHook() => Calls.Add("inherited-override");

        [BDTestRetrySetUp, BDTestRetryTearDown]
        protected override void AttributedHook() => Calls.Add("attributed-override");
    }

    private class AsyncBaseHooks : RunTestBase
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

    private sealed class NoHooks : RunTestBase;

    [BDTestRetry(3)]
    private sealed class FailingHooks(BDTestRun run, string hook, bool asynchronous, int failingRetry) : BDTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;
        public override BDTestRun TestRun => run;
        public InvalidOperationException Failure { get; } = new("retry hook failed");
        public int Attempts { get; private set; }

        public Task<BDTest.Contracts.ScenarioResult> RunScenario() => Given(() => FailStep()).When(() => FailStep()).Then(() => FailStep()).BDTestAsync();

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
