using System.Collections.Concurrent;
using BDTest.Test;
using Xunit;

namespace BDTest.xUnit.Tests;

public class XUnitTheoryTestsUniqueId : XUnitBDTestBase<XUnitContext>
{
    private static readonly ConcurrentDictionary<string, XUnitContext> Contexts = new();
    private static readonly ConcurrentDictionary<XUnitContext, string> ContextIds = new();
    private static string? _testCaseId;

    public static TheoryData<NonSerializableRow> Rows => new()
    {
        new NonSerializableRow(1),
        new NonSerializableRow(2),
        new NonSerializableRow(3),
    };

    // Force all non-serializable rows to be discovered at execution time under one test case.
    [Theory]
    [MemberData(nameof(Rows), DisableDiscoveryEnumeration = true)]
    public async Task NonSerializableRowsHaveSeparateExecutionIdsAndContexts(NonSerializableRow row)
    {
        var id = BDTestExecutionId;
        var context = Context;
        var testCaseId = TestContext.Current.TestCase!.UniqueID;
        var firstTestCaseId = Interlocked.CompareExchange(ref _testCaseId, testCaseId, null);
        Assert.Equal(firstTestCaseId ?? testCaseId, testCaseId);
        Assert.Equal(TestContext.Current.Test!.UniqueID, id);
        Assert.True(Contexts.TryAdd(id, context), $"Duplicate execution ID for row {row.Number}: {id}");
        Assert.True(ContextIds.TryAdd(context, id), $"Shared context for row {row.Number}");

        var scenario = await When(() => Yield())
            .Then(() => AssertContext(id, context))
            .BDTestAsync();

        Assert.Equal(id, scenario.FrameworkTestId);
        Assert.Equal(Status.Passed, scenario.Status);
        Assert.Single(BDTestUtil.GetScenarios(TestRun), s => s.FrameworkTestId == id);
    }

    private static async Task Yield() => await Task.Yield();

    private void AssertContext(string id, XUnitContext context)
    {
        Assert.Equal(id, BDTestContext.CurrentScenarioBDTestExecutionId);
        Assert.Same(context, Context);
    }

    public sealed class NonSerializableRow(int number)
    {
        public int Number { get; } = number;
    }
}
