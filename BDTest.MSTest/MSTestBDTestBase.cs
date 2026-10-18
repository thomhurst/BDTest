using BDTest.Test;
using BDTest.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class
{
    public TestContext TestContext { get; set; }

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => MSTestExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    [TestInitialize]
    public void InitializeBDTest()
    {
        MSTestExceptionRegistration.EnsureRegistered();
        // TestDisplayName includes the runner's data-row display name. The suffix also
        // distinguishes repeated executions with identical data and display names.
        if (TestContext != null && MSTestMethodIdentity.Current.Value == null)
            throw new InvalidOperationException("Replace [TestClass] with [BDTestClass] to supply MSTest's test MethodInfo.");
        BeginTest($"{TestContext?.FullyQualifiedTestClassName}.{TestContext?.TestDisplayName ?? TestContext?.TestName}_{Guid.NewGuid():N}",
            MSTestMethodIdentity.Current.Value);
    }

    [TestCleanup]
    public async Task BDTestMSTestTearDown() => await EndTestAsync();

    // Retain the synchronous entry point for existing callers.
    public void TearDown()
    {
        EndTest();
    }
}

// Shared by every generic context type; all registration is owned by the core.
internal static class MSTestExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => Assert.Inconclusive(reason))
        .Inconclusive<AssertInconclusiveException>();

    internal static void EnsureRegistered() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
