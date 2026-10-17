using System.Collections.Concurrent;

namespace BDTest.TUnit.Tests;

public class TUnitParallelTestsUniqueId : TUnitBDTestBase<TUnitContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();

    [Test]
    [Repeat(10)]
    public async Task UniqueId()
    {
        var id = BDTestContext.CurrentScenarioBDTestExecutionId;

        await Assert.That(id).IsEqualTo(TestContext.Current!.Id);
        await Assert.That(Ids.TryAdd(id, 0)).IsTrue();
    }
}
