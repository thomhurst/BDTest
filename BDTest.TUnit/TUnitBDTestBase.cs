using BDTest.Settings;
using BDTest.Test;
using TUnit.Core;
using TUnit.Core.Exceptions;
using TUnit.Core.Extensions;
using TUnitTestContext = TUnit.Core.TestContext;

namespace BDTest.TUnit;

public abstract class TUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class
{
    [Before(HookType.Class)]
    public static void SetupTUnitExceptions()
    {
        TUnitExceptionRegistration.EnsureRegistered();
    }

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => TUnitExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    // Keep the original public entry points, but give TUnit distinctive hook names.
    // Its generic-base hook invoker resolves methods by name on the derived type.
    [Before(HookType.Test)]
    public void BDTestTUnitSetup() => Setup();

    public void Setup()
    {
        BeginTest(TUnitTestContext.Current?.Id ?? Guid.NewGuid().ToString("N"),
            TUnitTestContext.Current?.Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
        // TUnit isolates hook execution contexts unless their AsyncLocal values are exported.
        TUnitTestContext.Current?.AddAsyncLocalValues();
    }

    [After(HookType.Test)]
    public async Task BDTestTUnitTearDown() => await EndTestAsync();

    public void TearDown()
    {
        EndTest();
    }
}

// Shared by every generic context type; all registration is owned by the core.
internal static class TUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => throw new SkipTestException(reason),
        reason => throw new InconclusiveTestException(reason))
        .Skip<SkipTestException>()
        .Inconclusive<InconclusiveTestException>();

    internal static void EnsureRegistered() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
