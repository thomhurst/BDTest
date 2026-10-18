using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ExecutionSchedulingTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
    public void FinishExecution() => EndTest();

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;
    private SynchronizationContext? _callerContext;
    private int _callerThread;
    private int _contextAttempts;

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

    [Test, BDTest.Attributes.BDTestRetry(1)]
    public async Task CallerContextSurvivesStepsHooksRetriesAndOutlineRows(
        [Values] bool directAwait, [Values] bool outline)
    {
        var scenario = await RunOnSingleThreadContext(async () =>
        {
            if (outline)
            {
                var builder = ScenarioOutline("Context rows", new[] { 1, 2 },
                    _ => Given(() => YieldOnCallerContext()).When(() => FailFirstContextAttempt())
                        .Then(() => YieldOnCallerContext()));
                return directAwait ? await builder : await builder.BDTestAsync();
            }

            var chain = Given(() => YieldOnCallerContext()).When(() => FailFirstContextAttempt())
                .Then(() => YieldOnCallerContext());
            return directAwait ? await chain : await chain.BDTestAsync();
        });

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(_contextAttempts, Is.EqualTo(outline ? 3 : 2));
        Assert.That(outline ? scenario.Examples[0].RetryCount : scenario.RetryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task StepCanOptOutWithoutMovingLaterStepsOffCallerContext()
    {
        var scenario = await RunOnSingleThreadContext(async () =>
            await Given(() => OptOutOfCallerContext()).When(() => YieldOnCallerContext())
                .Then(() => YieldOnCallerContext()));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
    }

    public override Task OnBeforeStep(Step step) => CheckHookContext();
    public override Task OnAfterStep(Step step) => CheckHookContext();
    public override Task OnBeforeRetry() => CheckHookContext();

    private Task CheckHookContext() => _callerContext == null ? Task.CompletedTask : YieldOnCallerContext();

    private async Task YieldOnCallerContext()
    {
        CheckCallerContext();
        await Task.Yield();
        CheckCallerContext();
    }

    private void CheckCallerContext()
    {
        Assert.That(SynchronizationContext.Current, Is.SameAs(_callerContext));
        Assert.That(Environment.CurrentManagedThreadId, Is.EqualTo(_callerThread));
    }

    private void FailFirstContextAttempt()
    {
        CheckCallerContext();
        if (++_contextAttempts == 1) throw new InvalidOperationException("Retry on caller context");
    }

    private async Task OptOutOfCallerContext()
    {
        CheckCallerContext();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Required(SynchronizationContext.Current).Post(_ => finish.SetResult(), null);
        await finish.Task.ConfigureAwait(false);
        Assert.That(SynchronizationContext.Current, Is.Null);
        Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(_callerThread));
    }

    private async Task<Scenario> RunOnSingleThreadContext(Func<Task<Scenario>> run)
    {
        var completed = new TaskCompletionSource<Scenario>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new PumpedSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            _callerContext = context;
            _callerThread = Environment.CurrentManagedThreadId;
            try
            {
                var execution = run();
                context.PumpUntilCompleted(execution);
                completed.SetResult(execution.GetAwaiter().GetResult());
            }
            catch (Exception exception) { completed.SetException(exception); }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        }) { IsBackground = true };
        thread.Start();
        try { return await completed.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { Assert.That(thread.Join(TimeSpan.FromSeconds(30)), Is.True, "Context thread did not exit."); }
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
    public async Task SynchronousRetriesDoNotAccumulateStackFrames([Values] bool directAwait)
    {
        var depths = new List<int>();
        var builder = Given(() => FailUntilLastAttempt(depths)).When(() => Console.WriteLine("retried"))
            .Then(() => Console.WriteLine("finished"));
        var scenario = directAwait ? await builder : await builder.BDTestAsync();

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

    private static void ChangeAmbient(AsyncLocal<string> ambient) => ambient.Value = "step";
    private static void CheckAmbient(AsyncLocal<string> ambient) => Assert.That(ambient.Value, Is.EqualTo("caller"));

    private sealed class PumpedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Add((callback, state));

        public void PumpUntilCompleted(Task execution)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!execution.IsCompleted)
            {
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Context execution did not finish.");
                if (_callbacks.TryTake(out var work, 50)) work.Callback(work.State);
            }
        }

        public void Dispose() => _callbacks.Dispose();
    }
}
