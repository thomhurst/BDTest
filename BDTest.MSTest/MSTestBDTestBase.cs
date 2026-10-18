using BDTest.Test;
using BDTest.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    public TestContext TestContext { get; set; }

    protected override void OnScenarioSkipped(string reason) => Assert.Inconclusive(reason);

    [TestInitialize]
    public void InitializeBDTest()
    {
        MSTestExceptionRegistration.EnsureRegistered();
        // TestDisplayName includes the runner's data-row display name. The suffix also
        // distinguishes repeated executions with identical data and display names.
        BeginTest($"{TestContext?.FullyQualifiedTestClassName}.{TestContext?.TestDisplayName ?? TestContext?.TestName}_{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void TearDown()
    {
        EndTest();
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
