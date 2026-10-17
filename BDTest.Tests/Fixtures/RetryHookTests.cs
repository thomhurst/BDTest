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

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task RetryFailureExposesOriginalHookExceptionAndStack(bool tearDown, bool asynchronous)
    {
        var fixture = new FailingHooks(tearDown, asynchronous);
        try
        {
            var error = await Assert.ThrowsAsync<ErrorOccurredDuringRetryActionException>(() => fixture.RunScenario());

            Assert.That(error.GetBaseException(), Is.SameAs(fixture.Failure));
            Assert.That(error.GetBaseException().StackTrace, Does.Contain(nameof(FailingHooks.ThrowHookFailure)));
            Assert.That(fixture.Attempts, Is.EqualTo(1), "A failing retry hook must stop the retry");
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PreservesPublicStaticHooks(bool tearDown)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunHooks(new StaticHooks(), tearDown));
        Assert.That(error.Message, Is.EqualTo("static hook ran"));
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

    [BDTestRetry(1)]
    private sealed class FailingHooks(bool tearDown, bool asynchronous) : BDTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;
        public InvalidOperationException Failure { get; } = new("retry hook failed");
        public int Attempts { get; private set; }

        public Task<Scenario> RunScenario() => Given(() => FailStep()).When(() => FailStep()).Then(() => FailStep()).BDTestAsync();

        private void FailStep()
        {
            Attempts++;
            throw new Exception("retry the scenario");
        }

        [BDTestRetrySetUp]
        public Task SetUp() => !tearDown ? ThrowHookFailure() : Task.CompletedTask;

        [BDTestRetryTearDown]
        public Task TearDown() => tearDown ? ThrowHookFailure() : Task.CompletedTask;

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

        public void Complete() => MarkTestAsComplete();
    }
}
