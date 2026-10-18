using System.Collections.Concurrent;

namespace BDTest.TUnit.Tests;

public class TUnitContextIsolationTests : TUnitBDTestBase<TUnitContext>
{
    private static readonly ConcurrentDictionary<TUnitContext, string> ContextOwners = new();

    [Test]
    [Repeat(10)]
    public async Task ParallelTestsHaveDistinctContextsThatPersistAcrossSteps()
    {
        var id = TestContext.Current!.Id;
        var context = Context;

        await Assert.That(context.OwnerTestId).IsNull();
        await Assert.That(ContextOwners.TryAdd(context, id)).IsTrue();
        context.OwnerTestId = id;

        var scenario = await Given(() => AssertContext(context, id))
            .When(() => AssertContextAfterYield(context, id))
            .Then(() => AssertContext(context, id))
            .BDTestAsync();

        await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
    }

    private async Task AssertContextAfterYield(TUnitContext expected, string id)
    {
        await Task.Yield();
        await AssertContext(expected, id);
    }

    private async Task AssertContext(TUnitContext expected, string id)
    {
        await Assert.That(ReferenceEquals(Context, expected)).IsTrue();
        await Assert.That(Context.OwnerTestId).IsEqualTo(id);
        await Assert.That(BDTestContext.CurrentScenarioBDTestExecutionId).IsEqualTo(id);
    }
}
