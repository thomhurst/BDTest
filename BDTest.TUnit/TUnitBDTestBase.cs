using BDTest.Settings;
using BDTest.Test;
using TUnit.Core;
using TUnit.Core.Exceptions;
using TUnitTestContext = TUnit.Core.TestContext;

namespace BDTest.TUnit;

public abstract class TUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    [Before(HookType.Class)]
    public static void SetupTUnitExceptions()
    {
        TUnitExceptionRegistration.EnsureRegistered();
    }

    protected override void OnScenarioSkipped(string reason) => throw new SkipTestException(reason);

    // Keep the original public entry points, but give TUnit distinctive hook names.
    // Its generic-base hook invoker resolves methods by name on the derived type.
    [Before(HookType.Test)]
    public void BDTestTUnitSetup() => Setup();

    public void Setup()
    {
        BeginTest(TUnitTestContext.Current?.Id ?? Guid.NewGuid().ToString("N"));
        // TUnit isolates hook execution contexts unless their AsyncLocal values are exported.
        TUnitTestContext.Current?.AddAsyncLocalValues();
    }

    [After(HookType.Test)]
    public void BDTestTUnitTearDown() => TearDown();

    public void TearDown()
    {
        EndTest();
    }
}

// Non-generic so all context types share the same CLR-protected initialization.
internal static class TUnitExceptionRegistration
{
    static TUnitExceptionRegistration()
    {
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(SkipTestException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(SkipTestException));
        }

        if (!BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(typeof(InconclusiveTestException)))
        {
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveTestException));
        }
    }

    internal static void EnsureRegistered() { }
}
