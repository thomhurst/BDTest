using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class ExecutionSchedulingTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;
    private SynchronizationContext? _callerContext;
    private int _callerThread;
    private int _contextAttempts;

    [Test]
    [MatrixDataSource]
    public async Task CompletedStepsExecuteInline([Matrix] bool directAwait)
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        var builder = Given(() => RecordThread(threads))
            .When(() => RecordThread(threads)).Then(() => RecordThread(threads));

        var task = directAwait ? AwaitBuilder(builder) : builder.BDTestAsync();
        var completedInline = task.IsCompletedSuccessfully;
        var scenario = await task;
        await Assert.That(completedInline).IsTrue();
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(threads).IsEquivalentTo(new[] { callerThread, callerThread, callerThread }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
            await Assert.That(task.IsCompleted).IsFalse();
            await Assert.That(events).IsEquivalentTo(new[] { "started" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        finally
        {
            finish.TrySetResult(true);
            await task.WaitAsync(TimeSpan.FromSeconds(20));
        }

        await Assert.That(events).IsEquivalentTo(new[] { "started", "finished", "when", "then" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required((await task).Steps).All(step => step.Status == Status.Passed)).IsTrue();
    }

    [Test, BDTest.Attributes.BDTestRetry(1)]
    [MatrixDataSource]
    public async Task CallerContextSurvivesStepsHooksRetriesAndOutlineRows(
        [Matrix] bool directAwait, [Matrix] bool outline)
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

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(_contextAttempts).IsEqualTo(outline ? 3 : 2);
        await Assert.That(outline ? Required(scenario.Examples)[0].RetryCount : scenario.RetryCount).IsEqualTo(1);
    }

    [Test]
    public async Task StepCanOptOutWithoutMovingLaterStepsOffCallerContext()
    {
        var scenario = await RunOnSingleThreadContext(async () =>
            await Given(() => OptOutOfCallerContext()).When(() => YieldOnCallerContext())
                .Then(() => YieldOnCallerContext()));
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
    }

    public override Task OnBeforeStep(BDTest.Contracts.StepResult step) => CheckHookContext();
    public override Task OnAfterStep(BDTest.Contracts.StepResult step) => CheckHookContext();
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
        RequireCondition(ReferenceEquals(SynchronizationContext.Current, _callerContext), "The caller synchronization context must be retained");
        RequireCondition(Environment.CurrentManagedThreadId == _callerThread, "The caller thread must be retained");
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
        await Assert.That(SynchronizationContext.Current).IsNull();
        await Assert.That(Environment.CurrentManagedThreadId).IsNotEqualTo(_callerThread);
    }

    private async Task<BDTest.Contracts.ScenarioResult> RunOnSingleThreadContext(Func<Task<BDTest.Contracts.ScenarioResult>> run)
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
        finally { await Assert.That(thread.Join(TimeSpan.FromSeconds(30))).IsTrue().Because("Context thread did not exit."); }
    }

    [Test]
    public async Task StepLocalAsyncLocalChangesDoNotLeakToOtherStepsOrCaller()
    {
        var ambient = new AsyncLocal<string> { Value = "caller" };
        await Given(() => ChangeAmbient(ambient))
            .When(() => CheckAmbient(ambient)).Then(() => CheckAmbient(ambient));
        await Assert.That(ambient.Value).IsEqualTo("caller");
    }

    [Test, BDTest.Attributes.BDTestRetry(64)]
    [MatrixDataSource]
    public async Task SynchronousRetriesDoNotAccumulateStackFrames([Matrix] bool directAwait)
    {
        var depths = new List<int>();
        var builder = Given(() => FailUntilLastAttempt(depths)).When(() => Console.WriteLine("retried"))
            .Then(() => Console.WriteLine("finished"));
        var scenario = directAwait ? await builder : await builder.BDTestAsync();

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.RetryCount).IsEqualTo(64);
        await Assert.That(depths).Count().IsEqualTo(65);
        // Allow runtime inlining differences, but not one nested executor per retry.
        await Assert.That(depths.Max() - depths.Min()).IsLessThan(20);
    }

    private static void FailUntilLastAttempt(List<int> depths)
    {
        depths.Add(new System.Diagnostics.StackTrace().FrameCount);
        if (depths.Count <= 64)
        {
            throw new InvalidOperationException("Retry synchronously");
        }
    }

    private static async Task<BDTest.Contracts.ScenarioResult> AwaitBuilder(RunnableStepBuilder builder) => await builder;

    private static void RecordThread(List<int> threads) => threads.Add(Environment.CurrentManagedThreadId);

    private static async Task HoldStep(TaskCompletionSource<bool> entered, Task finish, List<string> events)
    {
        events.Add("started");
        entered.SetResult(true);
        await finish;
        events.Add("finished");
    }

    private static void ChangeAmbient(AsyncLocal<string> ambient) => ambient.Value = "step";
    private static async Task CheckAmbient(AsyncLocal<string> ambient) => await Assert.That(ambient.Value).IsEqualTo("caller");

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
