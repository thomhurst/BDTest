using BDTest.Test;
using BDTest.Settings;
using Microsoft.Extensions.Time.Testing;

namespace BDTest.Tests.Fixtures;

public class AbandonedContextCleanupTests
{
    [Test]
    [Arguments("factory")]
    [Arguments("initialize")]
    [Arguments("step")]
    public async Task Teardown_returns_before_abandoned_work_and_disposes_only_after_it_settles(string phase)
    {
        var clock = new FakeTimeProvider();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workFinished = false;
        var disposedEarly = false;
        var disposals = 0;
        async Task Work()
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await finish.Task;
            workFinished = true;
        }
        var state = new State(phase == "initialize" ? Work : () => Task.CompletedTask, () =>
        {
            disposedEarly = !workFinished;
            Interlocked.Increment(ref disposals);
            disposed.TrySetResult(true);
        });
        var host = new Host(run, state, phase == "factory" ? Work : () => Task.CompletedTask);
        host.Start();
        try
        {
            var execution = host.When(phase == "step" ? Work : () => Task.CompletedTask)
                .Then(() => { }).WithScenarioTimeout(TimeSpan.FromSeconds(1)).BDTestAsync();
            await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<TimeoutException>();
            await host.EndTestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            await host.EndTestAsync();
            await Assert.That(disposals).IsZero();
            finish.TrySetResult(true);
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(disposedEarly).IsFalse();
            await Assert.That(disposals).IsEqualTo(1);
            await Assert.That(run.Scenarios.Single().Status).IsEqualTo(Status.Failed);
        }
        finally
        {
            finish.TrySetResult(true);
            await host.EndTestAsync();
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class Host(BDTestRun run, State state, Func<Task> create) : AbstractContextBDTestBase<State>
    {
        public override BDTestRun TestRun => run;
        internal void Start() => BeginTest(Guid.NewGuid().ToString("N"));
        protected override async ValueTask<State> CreateContextAsync(ContextCreation creation)
        {
            await create();
            return state;
        }
    }

    private sealed class State(Func<Task> initialize, Action dispose) : IBDTestContextLifetime, IDisposable
    {
        public async ValueTask InitializeAsync() => await initialize();
        public void Dispose() => dispose();
    }
}
