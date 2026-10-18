using BDTest.Test;
using BDTest.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    // MSTest creates a fixture instance per test, including each data-driven case.
    // An instance ID survives the separate execution flows used by async setup/body.
    protected override string BDTestExecutionId { get; } = Guid.NewGuid().ToString("N");

    protected override void OnScenarioSkipped(string reason) => Assert.Inconclusive(reason);

    [TestInitialize]
    public void InitializeBDTest()
    {
        MSTestExceptionRegistration.EnsureRegistered();
        MarkTestAsStarted();
    }

    [TestCleanup]
    public void TearDown()
    {
        RemoveContext();
        base.MarkTestAsComplete();
    }
}

// Non-generic so all context types share the same CLR-protected initialization.
internal static class MSTestExceptionRegistration
{
    static MSTestExceptionRegistration()
    {
        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(AssertInconclusiveException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(AssertInconclusiveException));
        }
    }

    internal static void EnsureRegistered() { }
}
