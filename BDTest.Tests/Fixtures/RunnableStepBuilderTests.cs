using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class RunnableStepBuilderTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    private int _executions;
    private int _retryHooks;

    [Test]
    public async Task BuildersExposeOnlyAsyncExecution()
    {
        await Assert.That(typeof(RunnableStepBuilder).GetMethod("BDTest")).IsNull();
        await Assert.That(typeof(ScenarioOutlineBuilder<>).GetMethod("BDTest")).IsNull();
    }

    [Test]
    [MatrixDataSource]
    public async Task RepeatedAndMixedEntryPointsRejectReuse(
        [Matrix] EntryPoint first, [Matrix] EntryPoint second)
    {
        var builder = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var scenario = await Execute(builder, first);

        var exception = await Assert.That(async () => await Execute(builder, second)).ThrowsExactly<AlreadyExecutedException>();

        await Assert.That(Required(exception).Message).Contains("builder");
        await Assert.That(_executions).IsEqualTo(1);
        await AssertReportedOnce(scenario);
    }

    [Test]
    [MatrixDataSource]
    public async Task FailedExecutionStillRejectsReuse([Matrix] EntryPoint first)
    {
        var failure = new InvalidOperationException("Original step failure");
        var builder = Given(() => CountAndThrow(failure)).When(() => NoOp()).Then(() => NoOp());

        var exception = await Assert.That(async () => await Execute(builder, first)).ThrowsExactly<InvalidOperationException>();
        var scenario = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == builder.TestId);
        foreach (var entryPoint in Enum.GetValues<EntryPoint>())
        {
            await Assert.That(async () => await Execute(builder, entryPoint)).ThrowsExactly<AlreadyExecutedException>();
        }

        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(_executions).IsEqualTo(1);
        await AssertReportedOnce(scenario);
    }

    [Test]
    [MatrixDataSource]
    public async Task ConcurrentMixedEntryPointsExecuteOnlyOncePerBuilder([Matrix] bool useContinuation)
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
        await Assert.That(rejectionCount).IsEqualTo(callerCount - expectedExecutions);
        await Assert.That(_executions).IsEqualTo(expectedExecutions);

        await Assert.That((completed).Length).IsEqualTo(expectedExecutions);
        var reported = BDTestUtil.GetScenarios(TestRun).Where(x => x.FrameworkTestId == builder.TestId).ToArray();

        await Assert.That(reported).IsEquivalentTo(completed, EqualityComparer<Scenario>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(completed.Select(x => x.Guid).Distinct().Count()).IsEqualTo(expectedExecutions);
    }

    [Test]
    [MatrixDataSource]
    public async Task ContinuationsExecuteOnceIndependently([Matrix] bool executeChildFirst)
    {
        var parent = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var child = parent.And(() => NoOp());
        RunnableStepBuilder first = executeChildFirst ? child : parent;
        RunnableStepBuilder second = executeChildFirst ? parent : child;

        var firstResult = await first;
        var secondResult = await second;
        await Assert.That(async () => await first).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(async () => await second).ThrowsExactly<AlreadyExecutedException>();

        await Assert.That(_executions).IsEqualTo(2);
        await Assert.That(firstResult.Guid).IsNotEqualTo(secondResult.Guid);

        await Assert.That(BDTestUtil.GetScenarios(TestRun).Where(x => x.FrameworkTestId == parent.TestId)).IsEquivalentTo(new[] { firstResult, secondResult }, EqualityComparer<Scenario>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task ExtendingExecutedBuilderPreservesOriginalReportAndExecutionGuard()
    {
        var parent = Given(() => CountExecution()).When(() => NoOp()).Then(() => NoOp());
        var original = await parent;
        var child = parent.And(() => NoOp());
        var extended = await child;

        await Assert.That(async () => await parent).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(async () => await child).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(_executions).IsEqualTo(2);
        await Assert.That(original.Steps).Count().IsEqualTo(3);
        await Assert.That(extended.Steps).Count().IsEqualTo(4);
        await Assert.That(Required(original.Steps).Intersect(Required(extended.Steps))).IsEmpty();

        await Assert.That(BDTestUtil.GetScenarios(TestRun).Where(x => x.FrameworkTestId == parent.TestId)).IsEquivalentTo(new[] { original, extended }, EqualityComparer<Scenario>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test, BDTestRetry(1)]
    public async Task InternalRetryCompletesBeforeBuilderRejectsReuse()
    {
        var builder = Given(() => FailFirstAttempt()).When(() => NoOp()).Then(() => NoOp());

        var scenario = await builder;
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(async () => await builder).ThrowsExactly<AlreadyExecutedException>();

        await Assert.That(_executions).IsEqualTo(2);
        await Assert.That(_retryHooks).IsEqualTo(1);
        await Assert.That(scenario.RetryCount).IsEqualTo(1);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await AssertReportedOnce(scenario);
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

    private async Task AssertReportedOnce(Scenario scenario)
    {
        var reported = BDTestUtil.GetScenarios(TestRun).Where(x => x.FrameworkTestId == scenario.FrameworkTestId).ToList();
        await Assert.That(reported).Count().IsEqualTo(1);
        // Re-execution used to overwrite the same report key with a different Scenario.
        await Assert.That(reported[0]).IsSameReferenceAs(scenario);
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
