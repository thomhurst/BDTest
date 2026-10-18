using System.Collections.Concurrent;
using BDTest.TestUtilities;

namespace BDTest.TUnit.Tests;

public class LifecycleTests : TUnitBDTestBase<TUnitContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();

    [Before(HookType.Test)]
    public void WriteBeforeFirstStep() => Console.WriteLine("startup:" + BDTestExecutionId);

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Repeat(2)]
    public async Task RowsAndRepeatsKeepIdentityAndStartupOutput(int row)
    {
        var id = BDTestExecutionId;
        var context = Context;
        await Assert.That(Ids.TryAdd(id, 0)).IsTrue();
        await Task.Yield();
        await Assert.That(BDTestExecutionId).IsEqualTo(id);
        await Assert.That(ReferenceEquals(Context, context)).IsTrue();
        var scenario = await When(() => GC.KeepAlive(row)).Then(() => NoOp()).BDTestAsync();
        await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
        await Assert.That(scenario.TestStartupInformation).Contains("startup:" + id);
    }

    [Test]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run).Actions);

    private static void NoOp() { }

    private sealed class Probe(BDTestRun run) : TUnitBDTestBase<TUnitContext>
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}
