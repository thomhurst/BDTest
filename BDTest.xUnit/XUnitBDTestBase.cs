using System.Reflection;
using System.Text;
using BDTest.Output;
using BDTest.Test;
using Xunit;
using Xunit.Abstractions;

namespace BDTest.xUnit;

public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable, IAsyncLifetime
    where TContext : class, new()
{
    private readonly string _frameworkTestId;
    private readonly StringBuilder _constructorOutput = new();

    protected override string BDTestExecutionId => _frameworkTestId;

    protected XUnitBDTestBase(ITestOutputHelper outputHelper)
    {
        var type = outputHelper.GetType();
        var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        var test = (ITest) testMember.GetValue(outputHelper);
        
        // v2 can share one test case across non-serializable theory rows.
        // Each row gets its own fixture instance; retain the case ID for correlation.
        _frameworkTestId = $"{test.TestCase.UniqueID}_{Guid.NewGuid():N}";
        TestOutputData.PendingStartupOutput = _constructorOutput;
        Console.SetOut(TestOutputData.Instance);
    }

    // Keep lifecycle binding outside the virtual async setup method: AsyncLocal
    // changes inside a derived async override do not flow back to xUnit's caller.
    Task IAsyncLifetime.InitializeAsync()
    {
        BeginTest(_frameworkTestId);
        lock (_constructorOutput)
        {
            TestRun.Store.AppendStartupOutput(BDTestExecutionId, _constructorOutput.ToString(), appendNewLine: false);
            _constructorOutput.Clear();
        }
        return InitializeAsync();
    }

    /// <summary>Runs async setup after the execution has been bound to the derived fixture's run.</summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    protected override void OnScenarioSkipped(string reason) => throw new NotSupportedException(
        "xUnit v2 does not support runtime skipping. Use [Fact(Skip = \"reason\")] or [Theory(Skip = \"reason\")] " +
        "to skip at discovery time. " + reason);

    public void Dispose()
    {
        EndTest();
        if (ReferenceEquals(TestOutputData.PendingStartupOutput, _constructorOutput))
            TestOutputData.PendingStartupOutput = null;
        lock (_constructorOutput) _constructorOutput.Clear();
    }
}
