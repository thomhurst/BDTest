using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using TUnit.Core;
using TUnit.Core.Exceptions;
using TUnitTestContext = TUnit.Core.TestContext;

namespace BDTest.TUnit;

public abstract class TUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    protected override string BDTestExecutionId => TUnitTestContext.Current!.Id;

    [Before(HookType.Class)]
    public static void SetupTUnitExceptions()
    {
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(SkipTestException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(SkipTestException));
        }

        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(InconclusiveTestException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveTestException));
        }
    }

    protected override void OnScenarioSkipped(string reason) => throw new SkipTestException(reason);

    [Before(HookType.Test)]
    public void Setup()
    {
        TestOutputData.FrameworkExecutionId = BDTestExecutionId;
    }

    [After(HookType.Test)]
    public void TearDown()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }
}
