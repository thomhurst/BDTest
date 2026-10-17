using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ExecutionSchedulingTests : BDTestBase
{
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    [Test]
    public async Task CompletedStepsExecuteInline([Values] bool directAwait)
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        var builder = Given(() => RecordThread(threads))
            .When(() => RecordThread(threads)).Then(() => RecordThread(threads));

        var task = directAwait ? AwaitBuilder(builder) : builder.BDTestAsync();
        var completedInline = task.IsCompletedSuccessfully;
        var scenario = await task;
        Assert.That(completedInline, Is.True);
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(threads, Is.EqualTo(new[] { callerThread, callerThread, callerThread }));
    }

    [Test]
    public async Task NextStepWaitsForAsyncCompletion()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<string>();
        var task = Given(() => HoldStep(entered, finish.Task, events))
            .When(() => events.Add("when")).Then(() => events.Add("then")).BDTestAsync();

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "started" }));
        }
        finally
        {
            finish.TrySetResult(true);
            await task.WaitAsync(TimeSpan.FromSeconds(20));
        }

        Assert.That(events, Is.EqualTo(new[] { "started", "finished", "when", "then" }));
        Assert.That((await task).Steps.All(step => step.Status == Status.Passed), Is.True);
    }

    [Test]
    public async Task SyncEntryPointDoesNotCaptureBlockedCallersContext([Values] bool fail)
    {
        var context = new QueuedSynchronizationContext();
        var completed = new TaskCompletionSource<Scenario>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ambient = new AsyncLocal<string>();
        var failure = new InvalidOperationException("Async step failure");
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            ambient.Value = "caller";
            try
            {
                var scenario = Given(() => YieldAndCheckAmbient(ambient, fail, failure))
                    .When(() => CheckAmbient(ambient)).Then(() => Console.WriteLine("last step"))
                    .BDTest();
                Console.WriteLine("caller teardown");
                completed.SetResult(scenario);
            }
            catch (Exception exception)
            {
                completed.SetException(exception);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }) { IsBackground = true };
        thread.Start();

        try
        {
            var winner = await Task.WhenAny(completed.Task, context.Posted.Task)
                .WaitAsync(TimeSpan.FromSeconds(20));
            Assert.That(winner, Is.SameAs(completed.Task), "Execution posted to the blocked caller's context.");
            if (fail)
            {
                var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () => await completed.Task);
                Assert.That(actual, Is.SameAs(failure));
                var scenario = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
                Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
                Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Failed));
                Assert.That(scenario.Steps.Skip(1).All(step => step.Status == Status.Inconclusive), Is.True);
            }
            else
            {
                var scenario = await completed.Task;
                Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
                Assert.That(scenario.Steps[0].Output, Is.EqualTo("async step"));
                Assert.That(scenario.Steps[2].Output, Is.EqualTo("last step"));
                Assert.That(scenario.TearDownOutput, Does.Contain("caller teardown"));
            }
        }
        finally
        {
            // If the regression returns, unblock the worker before failing the test.
            context.ReleaseToThreadPool();
            Assert.That(thread.Join(TimeSpan.FromSeconds(20)), Is.True, "Execution thread did not exit.");
        }
    }

    [Test]
    public async Task StepLocalAsyncLocalChangesDoNotLeakToOtherStepsOrCaller()
    {
        var ambient = new AsyncLocal<string> { Value = "caller" };
        await Given(() => ChangeAmbient(ambient))
            .When(() => CheckAmbient(ambient)).Then(() => CheckAmbient(ambient));
        Assert.That(ambient.Value, Is.EqualTo("caller"));
    }

    [Test, BDTest.Attributes.BDTestRetry(64)]
    public async Task SynchronousRetriesDoNotAccumulateStackFrames([Values] bool synchronous)
    {
        var depths = new List<int>();
        var builder = Given(() => FailUntilLastAttempt(depths)).When(() => Console.WriteLine("retried"))
            .Then(() => Console.WriteLine("finished"));
        var scenario = synchronous ? builder.BDTest() : await builder.BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(64));
        Assert.That(depths, Has.Count.EqualTo(65));
        // Allow runtime inlining differences, but not one nested executor per retry.
        Assert.That(depths.Max() - depths.Min(), Is.LessThan(20));
    }

    private static void FailUntilLastAttempt(List<int> depths)
    {
        depths.Add(new System.Diagnostics.StackTrace().FrameCount);
        if (depths.Count <= 64)
        {
            throw new InvalidOperationException("Retry synchronously");
        }
    }

    private static async Task<Scenario> AwaitBuilder(RunnableStepBuilder builder) => await builder;

    private static void RecordThread(List<int> threads) => threads.Add(Environment.CurrentManagedThreadId);

    private static async Task HoldStep(TaskCompletionSource<bool> entered, Task finish, List<string> events)
    {
        events.Add("started");
        entered.SetResult(true);
        await finish;
        events.Add("finished");
    }

    private static async Task YieldAndCheckAmbient(AsyncLocal<string> ambient, bool fail, Exception failure)
    {
        CheckAmbient(ambient);
        await Task.Yield();
        CheckAmbient(ambient);
        Console.WriteLine("async step");
        if (fail)
        {
            throw failure;
        }
    }

    private static void ChangeAmbient(AsyncLocal<string> ambient) => ambient.Value = "step";
    private static void CheckAmbient(AsyncLocal<string> ambient) => Assert.That(ambient.Value, Is.EqualTo("caller"));

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly List<Action> _callbacks = new();
        private bool _released;
        public TaskCompletionSource<bool> Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object state)
        {
            lock (_callbacks)
            {
                if (_released)
                {
                    ThreadPool.QueueUserWorkItem(_ => callback(state));
                }
                else
                {
                    _callbacks.Add(() => callback(state));
                }
                Posted.TrySetResult(true);
            }
        }

        public void ReleaseToThreadPool()
        {
            lock (_callbacks)
            {
                _released = true;
                foreach (var callback in _callbacks)
                {
                    ThreadPool.QueueUserWorkItem(_ => callback());
                }
                _callbacks.Clear();
            }
        }
    }
}
