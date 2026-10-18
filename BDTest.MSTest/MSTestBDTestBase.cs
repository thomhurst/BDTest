using BDTest.Test;
using BDTest.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

[TestClass]
public abstract class MSTestBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IBDTestMSTestTest
    where TContext : class
{
    private TestContext? _testContext;

    public TestContext? TestContext
    {
        get => _testContext;
        set
        {
            _testContext = value;
            if (value != null) MSTestBDTestLifecycle.Initialize(this, value);
        }
    }

    BDTestBase IBDTestMSTestTest.BDTestHost => this;

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => MSTestExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    // Compatibility entry points for callers that manually drive the old base lifecycle.
    public void InitializeBDTest()
    {
        if (TestContext != null)
        {
            MSTestBDTestLifecycle.Initialize(this, TestContext);
            return;
        }
        MSTestExceptionRegistration.EnsureRegistered();
        BeginTest(Guid.NewGuid().ToString("N"));
    }

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
