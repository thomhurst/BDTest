using System.Runtime.CompilerServices;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class AwaitableStepTests : RunTestBase
{
    [Test]
    [Arguments("Task", false)]
    [Arguments("Task", true)]
    [Arguments("Task<T>", false)]
    [Arguments("Task<T>", true)]
    [Arguments("ValueTask", false)]
    [Arguments("ValueTask", true)]
    [Arguments("ValueTask<T>", false)]
    [Arguments("ValueTask<T>", true)]
    public async Task Task_steps_wait_for_completion_and_propagate_asynchronous_exceptions(string kind, bool fail)
    {
        var work = new DelayedWork();
        var laterSteps = new List<string>();
        var when = kind switch
        {
            "Task" => When(() => work.RunTask()),
            "Task<T>" => When(() => work.RunGenericTask()),
            "ValueTask" => When(() => work.RunValueTask()),
            _ => When(() => work.RunGenericValueTask())
        };
        var execution = when.Then(() => laterSteps.Add("then")).BDTestAsync();
        try
        {
            await work.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(execution.IsCompleted).IsFalse();
            await Assert.That(laterSteps).IsEmpty();
            if (fail)
            {
                work.Finish.TrySetException(new InvalidOperationException("delayed failure"));
                var exception = await Assert.That(async () =>
                    await execution.WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<InvalidOperationException>();
                await Assert.That(Required(exception).Message).IsEqualTo("delayed failure");
                await Assert.That(laterSteps).IsEmpty();
            }
            else
            {
                work.Finish.TrySetResult();
                var scenario = await execution.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
                await Assert.That(laterSteps).IsEquivalentTo(new[] { "then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            }
            await Assert.That(work.Invocations).IsEqualTo(1);
        }
        finally
        {
            work.Complete();
            // Observe and drain work even if an assertion fails.
            try { await execution.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (InvalidOperationException) { }
        }
    }

    [Test]
    public async Task Synchronous_void_and_non_awaitable_result_steps_still_run()
    {
        var calls = new List<string>();
        var scenario = await Given(() => calls.Add("given"))
            .When(() => calls.Remove("given"))
            .Then(() => calls.Add("then"))
            .BDTestAsync();

        await Assert.That(calls).IsEquivalentTo(new[] { "then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
    }

    private static void NoOp() { }

    private sealed class DelayedWork
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Invocations { get; private set; }

        public void Complete() => Finish.TrySetResult();

        public async Task RunTask()
        {
            Invocations++;
            Started.TrySetResult();
            await Finish.Task;
        }

        public async Task<int> RunGenericTask()
        {
            await RunTask();
            return 42;
        }

        public ValueTask RunValueTask() => new(RunTask());
        public ValueTask<int> RunGenericValueTask() => new(RunGenericTask());
        public CustomAwaitable RunCustomAwaitable() => new(RunTask());
    }

    private readonly struct CustomAwaitable(Task task)
    {
        public TaskAwaiter GetAwaiter() => task.GetAwaiter();
        // An unrelated overload must not turn validation into AmbiguousMatchException.
        public TaskAwaiter GetAwaiter(int unused) => task.GetAwaiter();
    }
}
