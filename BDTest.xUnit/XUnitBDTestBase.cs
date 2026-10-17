using System.Reflection;
using BDTest.Output;
using BDTest.Test;
using Xunit.Abstractions;

namespace BDTest.xUnit;

public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable
    where TContext : class, new()
{
    private readonly ITest _test;

    protected XUnitBDTestBase(ITestOutputHelper outputHelper)
    {
        var type = outputHelper.GetType();
        var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        _test = (ITest) testMember.GetValue(outputHelper);
        
        BDTestExecutionId = _test.TestCase.UniqueID;
        TestOutputData.FrameworkExecutionId = BDTestExecutionId;
    }

    protected override string BDTestExecutionId { get; }

    protected override void OnScenarioSkipped(string reason) => throw new NotSupportedException(
        "xUnit v2 does not support runtime skipping. Use [Fact(Skip = \"reason\")] or [Theory(Skip = \"reason\")] " +
        "to skip at discovery time. " + reason);

    public void Dispose()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }
}