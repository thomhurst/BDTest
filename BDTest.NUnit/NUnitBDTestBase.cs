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

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => NUnitExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

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

// Shared by every generic context type; all registration is owned by the core.
internal static class NUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => Assert.Ignore(reason), reason => Assert.Inconclusive(reason))
        .Success<SuccessException>()
        .Skip<IgnoreException>()
        .Inconclusive<InconclusiveException>();

    internal static void EnsureRegistered() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
