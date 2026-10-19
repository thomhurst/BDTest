using BDTest.Engine;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class AttributedHookCancellationTests
{
    [Test]
    public async Task Cancelled_retry_hook_is_classified_by_the_execution_wrapper()
    {
        using var run = BDTestRun.Start();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var host = new Host(run);
        var scenario = new Scenario(new(), new ScenarioDefinition { Run = run, Guid = "scenario", TestId = "test" });
        using var cancellation = new ExecutionCancellation(scenario, source.Token, default);
        var wrapperCalled = false;

        await Assert.That(async () => await host.RunMethodWithAttribute<BDTestRetrySetUpAttribute>(source.Token, invoke =>
        {
            wrapperCalled = true;
            return cancellation.RunAsync(invoke);
        })).Throws<OperationCanceledException>();

        await Assert.That(wrapperCalled).IsTrue();
        await Assert.That(scenario.ExecutionCancelled).IsTrue();
        await Assert.That(host.HookCalled).IsFalse();
    }

    [Test]
    public async Task Cancelled_retry_hook_without_a_wrapper_does_not_run()
    {
        using var run = BDTestRun.Start();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var host = new Host(run);

        await Assert.That(async () => await host.RunMethodWithAttribute<BDTestRetrySetUpAttribute>(source.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(host.HookCalled).IsFalse();
    }

    private sealed class Host(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        internal bool HookCalled;

        [BDTestRetrySetUp]
        public void BeforeRetry() => HookCalled = true;
    }
}
