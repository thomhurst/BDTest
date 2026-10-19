using Status = BDTest.Contracts.Status;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class RuntimeContractTests
{
    private static T Required<T>(T? value) where T : class =>
        value ?? throw new AssertionException("Expected a non-null test value.");

    private static bool _initialized;

    [ModuleInitializer]
    public static void Initialize() => _initialized = true;

    [Test]
    public void Consumer_module_initializer_works_without_a_public_polyfill()
    {
        Assert.That(_initialized, Is.True);
        Assert.That(typeof(BDTestRun).Assembly.GetExportedTypes().Any(type =>
            type.Namespace == "System.Runtime.CompilerServices"), Is.False);
#if LEGACY_CORE
        const string expected = ".NETStandard,Version=v2.0";
#else
        const string expected = ".NETCoreApp,Version=v8.0";
#endif
        Assert.That(typeof(BDTestRun).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName, Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Timed_execution_preserves_user_exceptions(bool timeoutException)
    {
        using var run = BDTestRun.Start();
        var test = new TestHost(run);
        var expected = timeoutException ? (Exception)new TimeoutException("user timeout") : new OperationCanceledException("user cancellation");
        var builder = test.When(() => Fail(expected)).WithStepTimeout(TimeSpan.FromSeconds(10)).Then(() => { });
        var actual = await Assert.ThrowsAsync(expected.GetType(), async () => await builder.BDTestAsync());
        Assert.That(actual, Is.SameAs(expected));
        Assert.That(run.Scenarios.Single().TimedOut, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Expired_timeout_cancels_work_and_records_failure(bool scenarioTimeout)
    {
        using var run = BDTestRun.Start();
        var test = new TestHost(run);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterRan = false;
        var waitingStep = test.When(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled.TrySetResult(); }
        });
        var configured = scenarioTimeout
            ? waitingStep.WithScenarioTimeout(TimeSpan.FromMilliseconds(100))
            : waitingStep.WithStepTimeout(TimeSpan.FromMilliseconds(100));
        var builder = configured.Then(() => { laterRan = true; });
        await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var scenario = run.Scenarios.Single();
        Assert.That(scenario.TimedOut, Is.True);
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps)[0].TimedOut, Is.True);
        Assert.That(laterRan, Is.False);
        Assert.That(scenario.TimeTaken, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(scenario.StartTime.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task Late_user_failure_does_not_replace_timeout_result()
    {
        using var run = BDTestRun.Start();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = new TestHost(run).When(async () =>
        {
            try
            {
                await finish.Task;
                throw new InvalidOperationException("late failure");
            }
            finally { completed.TrySetResult(); }
        }).WithStepTimeout(TimeSpan.FromMilliseconds(100)).Then(() => { });
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            finish.TrySetResult();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var scenario = run.Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        var steps = Required(scenario.Steps);
        var exception = steps[0].Exception ?? throw new AssertionException("Expected a timeout exception.");
        Assert.That(exception.TypeName, Does.Contain(nameof(TimeoutException)));
        Assert.That(exception.Message, Does.Contain("timed out"));
    }

    [Test]
    public async Task Successful_timed_execution_records_utc_and_elapsed_time()
    {
        using var run = BDTestRun.Start();
        var scenario = await new TestHost(run).When(async () => await Task.Delay(10))
            .WithStepTimeout(TimeSpan.FromSeconds(10)).Then(() => { }).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.TimedOut, Is.False);
        Assert.That(scenario.StartTime.Offset, Is.EqualTo(TimeSpan.Zero));
        Assert.That(scenario.EndTime, Is.GreaterThanOrEqualTo(scenario.StartTime));
        Assert.That(scenario.TimeTaken, Is.GreaterThanOrEqualTo(Required(scenario.Steps)[0].TimeTaken));
    }

    private static void Fail(Exception exception) => throw exception;
    private sealed class TestHost(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
