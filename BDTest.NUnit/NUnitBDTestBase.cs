using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;
using BDTest.Maps;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace BDTest.NUnit;

[TestFixture]
public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
{
    protected override string BDTestExecutionId => NUnitTestContext.CurrentContext.Test.ID;

    [OneTimeSetUp]
    public static void SetupNUnitExceptions()
    {
        if (!BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(typeof(SuccessException)))
        {
            BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(SuccessException));
        }
            
        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(IgnoreException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(IgnoreException));
        }
            
        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(InconclusiveException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveException));
        }
    }

    [TearDown]
    public void PruneContext()
    {
        MarkPassIfRetriedSuccessfully();
        RemoveContext();
    }

    private void MarkPassIfRetriedSuccessfully()
    {
        var allScenarios = TestHolder.ScenariosByTestFrameworkId;
        if (allScenarios != null && allScenarios.TryGetValue(BDTestExecutionId, out var scenario) && scenario?.Status == Status.Passed)
        {
            TestExecutionContext.CurrentContext.CurrentResult.SetResult(ResultState.Success);
        }
    }

    [TearDown]
    protected override void MarkTestAsComplete()
    {
        base.MarkTestAsComplete();
    }
}