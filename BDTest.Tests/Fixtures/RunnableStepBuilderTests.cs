using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RunnableStepBuilderTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
    public void FinishExecution() => EndTest();

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    private int _executions;
    private int _retryHooks;

    [Test]
    public void BuildersExposeOnlyAsyncExecution()
    {
        Assert.That(typeof(RunnableStepBuilder).GetMethod("BDTest"), Is.Null);
        Assert.That(typeof(ScenarioOutlineBuilder<>).GetMethod("BDTest"), Is.Null);
    }

    [Test]
    public async Task RepeatedAndMixedEntryPointsRejectReuse(
        [Values] EntryPoint first, [Values] EntryPoint second)
    {
        var builder = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var scenario = await Execute(builder, first);

        var exception = await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await Execute(builder, second));

        Assert.That(Required(exception).Message, Does.Contain("builder"));
        Assert.That(_executions, Is.EqualTo(1));
        AssertReportedOnce(scenario);
    }

    [Test]
    public async Task FailedExecutionStillRejectsReuse([Values] EntryPoint first)
    {
        var failure = new InvalidOperationException("Original step failure");
        var builder = Given(() => CountAndThrow(failure)).When(() => NoOp()).Then(() => NoOp());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Execute(builder, first));
        var scenario = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == builder.TestId);
        foreach (var entryPoint in Enum.GetValues<EntryPoint>())
        {
            await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await Execute(builder, entryPoint));
        }

        Assert.That(exception, Is.SameAs(failure));
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(_executions, Is.EqualTo(1));
        AssertReportedOnce(scenario);
    }

    [Test]
    public async Task ConcurrentMixedEntryPointsExecuteOnlyOncePerBuilder([Values] bool useContinuation)
    {
        const int callerCount = 12;
        var expectedExecutions = useContinuation ? 2 : 1;
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rejected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rejectionCount = 0;
        var builder = Given(() => HoldExecution(entered, finish.Task)).When(() => NoOp()).Then(() => NoOp());
        RunnableStepBuilder continuation = useContinuation ? builder.And(() => NoOp()) : builder;
        var callers = Enumerable.Range(0, callerCount).Select(index => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                return await Execute(index % 2 == 0 ? builder : continuation, (EntryPoint)((index / 2) % 2));
            }
            catch (AlreadyExecutedException)
            {
                if (Interlocked.Increment(ref rejectionCount) == callerCount - expectedExecutions)
                {
                    rejected.SetResult(true);
                }
                return null;
            }
        })).ToArray();

        start.SetResult(true);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            // All other calls must fail while the first execution is still running.
            await rejected.Task.WaitAsync(TimeSpan.FromSeconds(20));
            // Rejections prove the guards are claimed; the winners may still be entering their steps.
        }
        finally
        {
            finish.TrySetResult(true);
            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(20));
        }

        var scenarios = await Task.WhenAll(callers);
        var completed = scenarios.OfType<Scenario>().ToArray();
        Assert.That(rejectionCount, Is.EqualTo(callerCount - expectedExecutions));
        Assert.That(_executions, Is.EqualTo(expectedExecutions));
        Assert.That(completed, Has.Length.EqualTo(expectedExecutions));
        var reported = BDTestUtil.GetScenarios().Where(x => x.FrameworkTestId == builder.TestId).ToArray();
        Assert.That(reported, Is.EquivalentTo(completed));
        Assert.That(completed.Select(x => x.Guid).Distinct().Count(), Is.EqualTo(expectedExecutions));
    }

    [Test]
    public async Task ContinuationsExecuteOnceIndependently([Values] bool executeChildFirst)
    {
        var parent = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var child = parent.And(() => NoOp());
        RunnableStepBuilder first = executeChildFirst ? child : parent;
        RunnableStepBuilder second = executeChildFirst ? parent : child;

        var firstResult = await first;
        var secondResult = await second;
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await first);
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await second);

        Assert.That(_executions, Is.EqualTo(2));
        Assert.That(firstResult.Guid, Is.Not.EqualTo(secondResult.Guid));
        Assert.That(BDTestUtil.GetScenarios().Where(x => x.FrameworkTestId == parent.TestId),
            Is.EquivalentTo(new[] { firstResult, secondResult }));
    }

    [Test]
    public async Task ExtendingExecutedBuilderPreservesOriginalReportAndExecutionGuard()
    {
        var parent = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var original = await parent;
        var child = parent.And(() => NoOp());
        var extended = await child;

        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await parent);
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await child);
        Assert.That(_executions, Is.EqualTo(2));
        Assert.That(original.Steps, Has.Count.EqualTo(3));
        Assert.That(extended.Steps, Has.Count.EqualTo(4));
        Assert.That(Required(original.Steps).Intersect(Required(extended.Steps)), Is.Empty);
        Assert.That(BDTestUtil.GetScenarios().Where(x => x.FrameworkTestId == parent.TestId),
            Is.EquivalentTo(new[] { original, extended }));
    }

    [Test, BDTestRetry(1)]
    public async Task InternalRetryCompletesBeforeBuilderRejectsReuse()
    {
        var builder = Given(() => FailFirstAttempt()).When(() => NoOp()).Then(() => NoOp());

        var scenario = await builder;
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder.BDTestAsync());
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder);

        Assert.That(_executions, Is.EqualTo(2));
        Assert.That(_retryHooks, Is.EqualTo(1));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        AssertReportedOnce(scenario);
    }

    public override Task OnBeforeRetry()
    {
        _retryHooks++;
        return Task.CompletedTask;
    }

    private static async Task<Scenario> Execute(RunnableStepBuilder builder, EntryPoint entryPoint)
    {
        return entryPoint switch
        {
            EntryPoint.Asynchronous => await builder.BDTestAsync(),
            EntryPoint.DirectAwait => await builder,
            _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
        };
    }

    private static void AssertReportedOnce(Scenario scenario)
    {
        var reported = BDTestUtil.GetScenarios().Where(x => x.FrameworkTestId == scenario.FrameworkTestId).ToList();
        Assert.That(reported, Has.Count.EqualTo(1));
        // Re-execution used to overwrite the same report key with a different Scenario.
        Assert.That(reported[0], Is.SameAs(scenario));
    }

    private void CountExecution() => Interlocked.Increment(ref _executions);

    private void CountAndThrow(Exception exception)
    {
        CountExecution();
        throw exception;
    }

    private async Task HoldExecution(TaskCompletionSource<bool> entered, Task finish)
    {
        CountExecution();
        entered.TrySetResult(true);
        await finish;
    }

    private void FailFirstAttempt()
    {
        CountExecution();
        if (_executions == 1)
        {
            throw new InvalidOperationException("Retry this attempt");
        }
    }

    private static void NoOp() { }

    public enum EntryPoint
    {
        Asynchronous,
        DirectAwait
    }
}
