using BDTest.Settings;
using BDTest.Test;
using TUnit.Core;
using TUnit.Core.Exceptions;
using TUnitTestContext = TUnit.Core.TestContext;

namespace BDTest;

public abstract class TUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IBDTestTUnitTest
    where TContext : class
{
    private BDTestRun? _assemblyRun;

    /// <summary>The configured run owned by this fixture's assembly, or an explicit override.</summary>
    public override BDTestRun TestRun => _assemblyRun ??= TUnitBDTestAssembly.GetRun(GetType().Assembly);

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => TUnitExceptionRegistration.OutcomeMap;

    BDTestBase IBDTestTUnitTest.BDTestHost => this;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    // Compatibility entry points for custom harnesses. Native execution uses global hooks.
    public void BDTestTUnitSetup() => Setup();

    public void Setup()
    {
        TUnitBDTestLifecycle.Begin(this, TUnitTestContext.Current);
    }

    public async Task BDTestTUnitTearDown() => await EndTestAsync();

    public void TearDown()
    {
        EndTest();
    }
}

// Immutable declaration shared by every context type; registrations belong to each run.
internal static class TUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => throw new SkipTestException(reason),
        reason => throw new InconclusiveTestException(reason))
        .Skip<SkipTestException>()
        .Inconclusive<InconclusiveTestException>();
}
