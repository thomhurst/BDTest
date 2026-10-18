using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class AwaitableStepTests : BDTestBase
{
    [TestCase("ValueTask")]
    [TestCase("ValueTask<T>")]
    [TestCase("custom")]
    [TestCase("configured Task")]
    [TestCase("configured ValueTask")]
    [TestCase("Task as Action")]
    public async Task Discarded_awaitables_are_rejected_before_the_step_is_invoked(string kind)
    {
        var work = new DelayedWork();
        #pragma warning disable CS4014 // Deliberately reproduce a Task call bound to Action.
        Expression<Action> step = kind switch
        {
            "ValueTask" => () => work.RunValueTask(),
            "ValueTask<T>" => () => work.RunGenericValueTask(),
            "custom" => () => work.RunCustomAwaitable(),
            "configured Task" => () => work.RunTask().ConfigureAwait(false),
            "configured ValueTask" => () => work.RunValueTask().ConfigureAwait(false),
            _ => () => work.RunTask()
        };
        #pragma warning restore CS4014
        var laterSteps = new List<string>();
        var test = Given(step).When(() => laterSteps.Add("when")).Then(() => laterSteps.Add("then"));

        try
        {
            // C# binds these method calls to Action, but retains the result type in the body.
            Assert.That(step.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(step.Body.Type, Is.Not.EqualTo(typeof(void)));
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await test.BDTestAsync());

            Assert.That(exception.Message, Does.Contain(step.Body.Type.FullName).And.Contain("Task").And.Contain("AsTask()"));
            Assert.That(work.Invocations, Is.Zero);
            Assert.That(laterSteps, Is.Empty);
            var scenario = BDTestUtil.GetScenarios().Single(result =>
                result.FrameworkTestId == test.TestId && result.GetScenarioText() == test.GetScenarioText());
            Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
            Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Failed));
            Assert.That(scenario.Steps[0].Exception.Message, Is.EqualTo(exception.Message));
            Assert.That(scenario.Steps.Skip(1).All(stepResult => stepResult.Status == Status.NotRun), Is.True);
        }
        finally
        {
            work.Complete();
        }
    }

    [TestCase("ValueTask")]
    [TestCase("ValueTask<T>")]
    public async Task ValueTask_method_calls_bind_without_adding_public_overloads(string kind)
    {
        var work = new DelayedWork();
        // Exercise overload selection at the public call site, without an explicit Action cast.
        var test = kind == "ValueTask"
            ? When(() => work.RunValueTask()).Then(() => NoOp())
            : When(() => work.RunGenericValueTask()).Then(() => NoOp());
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await test.BDTestAsync());
            Assert.That(work.Invocations, Is.Zero);
        }
        finally
        {
            work.Complete();
        }
    }

    [TestCase("Task", false)]
    [TestCase("Task", true)]
    [TestCase("Task<T>", false)]
    [TestCase("Task<T>", true)]
    [TestCase("ValueTask.AsTask", false)]
    [TestCase("ValueTask.AsTask", true)]
    [TestCase("ValueTask<T>.AsTask", false)]
    [TestCase("ValueTask<T>.AsTask", true)]
    public async Task Task_steps_wait_for_completion_and_propagate_asynchronous_exceptions(string kind, bool fail)
    {
        var work = new DelayedWork();
        var laterSteps = new List<string>();
        var when = kind switch
        {
            "Task" => When(() => work.RunTask()),
            "Task<T>" => When(() => work.RunGenericTask()),
            "ValueTask.AsTask" => When(() => work.RunValueTask().AsTask()),
            _ => When(() => work.RunGenericValueTask().AsTask())
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
                Assert.That(exception.Message, Is.EqualTo("delayed failure"));
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

    [TestCase(typeof(int))]
    [TestCase(typeof(MissingNotification))]
    [TestCase(typeof(MissingCompletion))]
    [TestCase(typeof(NonBooleanCompletion))]
    [TestCase(typeof(PrivateCompletionGetter))]
    [TestCase(typeof(MissingResult))]
    [TestCase(typeof(GenericResult))]
    [TestCase(typeof(HiddenCompletion))]
    [TestCase(typeof(HiddenCompletionField))]
    [TestCase(typeof(IHiddenCompletion))]
    public async Task Unrelated_GetAwaiter_methods_do_not_prevent_synchronous_steps(Type awaiterType)
    {
        var calls = new List<string>();
        var method = GetType().GetMethod(nameof(ReturnSynchronousResult),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(awaiterType);
        var step = Expression.Lambda<Action>(Expression.Call(method, Expression.Constant(calls)));

        var scenario = await When(step).Then(() => calls.Add("then")).BDTestAsync();

        Assert.That(calls, Is.EqualTo(new[] { "step", "then" }));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
    }

    private static SynchronousResult<TAwaiter> ReturnSynchronousResult<TAwaiter>(List<string> calls)
    {
        calls.Add("step");
        return new();
    }

    private sealed class SynchronousResult<TAwaiter>
    {
        public TAwaiter GetAwaiter() => throw new AssertionException("GetAwaiter must not be invoked.");
    }

    private sealed class MissingNotification
    {
        public bool IsCompleted => true;
        public void GetResult() { }
    }

    private sealed class MissingCompletion : INotifyCompletion
    {
        public void OnCompleted(Action continuation) { }
        public void GetResult() { }
    }

    private sealed class NonBooleanCompletion : INotifyCompletion
    {
        public int IsCompleted => 1;
        public void OnCompleted(Action continuation) { }
        public void GetResult() { }
    }

    private sealed class PrivateCompletionGetter : INotifyCompletion
    {
        public bool IsCompleted { private get; set; }
        public void OnCompleted(Action continuation) { }
        public void GetResult() { }
    }

    private sealed class MissingResult : INotifyCompletion
    {
        public bool IsCompleted => true;
        public void OnCompleted(Action continuation) { }
    }

    private sealed class GenericResult : INotifyCompletion
    {
        public bool IsCompleted => true;
        public void OnCompleted(Action continuation) { }
        public void GetResult<T>() { }
    }

    [TestCase(typeof(CompleteAwaiter))]
    [TestCase(typeof(InheritedCompletion))]
    [TestCase(typeof(ICompleteAwaiter))]
    public async Task Inherited_awaiter_properties_still_cause_rejection(Type awaiterType)
    {
        var calls = new List<string>();
        var method = GetType().GetMethod(nameof(ReturnSynchronousResult),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(awaiterType);
        var step = Expression.Lambda<Action>(Expression.Call(method, Expression.Constant(calls)));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await When(step).Then(() => calls.Add("then")).BDTestAsync());

        Assert.That(calls, Is.Empty);
    }

    private interface ICompleteAwaiter : INotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }

    private interface IHiddenCompletion : ICompleteAwaiter
    {
        new int IsCompleted { get; }
    }

    private sealed class InheritedCompletion : CompleteAwaiter { }

    private class CompleteAwaiter : INotifyCompletion
    {
        public bool IsCompleted => true;
        public void OnCompleted(Action continuation) { }
        public void GetResult() { }
    }

    private sealed class HiddenCompletion : CompleteAwaiter
    {
        public new int IsCompleted => 1;
    }

    private sealed class HiddenCompletionField : CompleteAwaiter
    {
        public new bool IsCompleted = true;
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
