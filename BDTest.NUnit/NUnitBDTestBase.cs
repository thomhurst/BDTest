using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;
using BDTest.Maps;
using BDTest.Output;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace BDTest.NUnit;

[TestFixture]
public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
{
    protected override string BDTestExecutionId =>
        $"{NUnitTestContext.CurrentContext.Test.ID}_{NUnitTestContext.CurrentContext.CurrentRepeatCount}";

    [OneTimeSetUp]
    public static void SetupNUnitExceptions()
    {
        NUnitExceptionRegistration.EnsureRegistered();
    }

    protected override void OnScenarioSkipped(string reason) => Assert.Ignore(reason);

    [SetUp]
    public void Setup()
    {
        TestOutputData.FrameworkExecutionId = BDTestExecutionId;
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

// Non-generic so all context types share the same CLR-protected initialization.
internal static class NUnitExceptionRegistration
{
    static NUnitExceptionRegistration()
    {
        if (!BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(typeof(SuccessException)))
        {
            BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(SuccessException));
        }

        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(IgnoreException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(IgnoreException));
        }

        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(InconclusiveException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveException));
        }
    }

    internal static void EnsureRegistered() { }
}
