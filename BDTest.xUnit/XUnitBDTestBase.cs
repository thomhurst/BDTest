using System.Reflection;
using BDTest.Test;
using Xunit.Abstractions;

namespace BDTest.xUnit;

public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable
    where TContext : class, new()
{
    protected XUnitBDTestBase(ITestOutputHelper outputHelper)
    {
        var type = outputHelper.GetType();
        var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        var test = (ITest) testMember.GetValue(outputHelper);
        
        // v2 can share one test case across non-serializable theory rows.
        // Each row gets its own fixture instance; retain the case ID for correlation.
        BeginTest($"{test.TestCase.UniqueID}_{Guid.NewGuid():N}");
    }

    protected override void OnScenarioSkipped(string reason) => throw new NotSupportedException(
        "xUnit v2 does not support runtime skipping. Use [Fact(Skip = \"reason\")] or [Theory(Skip = \"reason\")] " +
        "to skip at discovery time. " + reason);

    public void Dispose()
    {
        EndTest();
    }
}
