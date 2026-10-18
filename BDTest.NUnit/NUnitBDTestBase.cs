using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace BDTest.NUnit;

[TestFixture]
public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
{
    [OneTimeSetUp]
    public static void SetupNUnitExceptions()
    {
        NUnitExceptionRegistration.EnsureRegistered();
    }

    protected override void OnScenarioSkipped(string reason) => Assert.Ignore(reason);

    [SetUp]
    public void Setup()
    {
        BeginTest($"{NUnitTestContext.CurrentContext.Test.ID}_{NUnitTestContext.CurrentContext.CurrentRepeatCount}");
    }

    // Kept callable for existing consumers; NUnit teardown uses MarkTestAsComplete.
    public void PruneContext()
    {
        try
        {
            MarkPassIfRetriedSuccessfully();
        }
        finally
        {
            EndTest();
        }
    }

    private void MarkPassIfRetriedSuccessfully()
    {
        var allScenarios = TestRun.Store.ScenariosByTestFrameworkId;
        if (allScenarios.TryGetValue(BDTestExecutionId, out var scenario) && scenario?.Status == Status.Passed)
        {
            TestExecutionContext.CurrentContext.CurrentResult.SetResult(ResultState.Success);
        }
    }
    // Retain the virtual hook so existing NUnit completion overrides still run.
    [TearDown]
    [Obsolete("Call EndTest() in new adapters. Retained for NUnit teardown compatibility.")]
    protected override void MarkTestAsComplete()
    {
        PruneContext();
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
