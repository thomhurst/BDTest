using System.Runtime.CompilerServices;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class AwaitableStepTests : BDTestBase
{
    [TestCase("Task", false)]
    [TestCase("Task", true)]
    [TestCase("Task<T>", false)]
    [TestCase("Task<T>", true)]
    [TestCase("ValueTask", false)]
    [TestCase("ValueTask", true)]
    [TestCase("ValueTask<T>", false)]
    [TestCase("ValueTask<T>", true)]
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
            Assert.That(execution.IsCompleted, Is.False);
            Assert.That(laterSteps, Is.Empty);
            if (fail)
            {
                work.Finish.TrySetException(new InvalidOperationException("delayed failure"));
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await execution.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.That(Required(exception).Message, Is.EqualTo("delayed failure"));
                Assert.That(laterSteps, Is.Empty);
            }
            else
            {
                work.Finish.TrySetResult();
                var scenario = await execution.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
                Assert.That(laterSteps, Is.EqualTo(new[] { "then" }));
            }
            Assert.That(work.Invocations, Is.EqualTo(1));
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

        Assert.That(calls, Is.EqualTo(new[] { "then" }));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
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
