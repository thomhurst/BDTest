using System.Collections.Concurrent;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.All)]
public class NUnitLifecycleTests : NUnitBDTestBase<MyTestContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();

    [SetUp]
    public void WriteBeforeFirstStep() => Console.WriteLine("startup:" + BDTestExecutionId);

    [TestCase(1)]
    [TestCase(2)]
    [Repeat(2)]
    public async Task RowsAndRepeatsKeepTheirIdentityAndStartupOutput(int row)
    {
        var id = BDTestExecutionId;
        var context = Context;
        Assert.That(Ids.TryAdd(id, 0), Is.True, $"Duplicate execution ID for row {row}: {id}");
        await Task.Yield();
        Assert.That(BDTestExecutionId, Is.EqualTo(id));
        Assert.That(Context, Is.SameAs(context));
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.FrameworkTestId, Is.EqualTo(id));
        Assert.That(scenario.TestStartupInformation, Does.Contain("startup:" + id));
    }

    private static void NoOp() { }

    [Test]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run).Actions);

    private sealed class Probe(BDTestRun run) : NUnitBDTestBase<MyTestContext>
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}
