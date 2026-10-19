using BDTest.Test;
using BDTest.Settings;

namespace BDTest;

/// <summary>Creates composition hosts using the assembly-owned or an explicitly supplied run.</summary>
public static class TUnitBDTestScenario
{
    /// <summary>Register these outcomes before starting an explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => TUnitExceptionRegistration.OutcomeMap;

    /// <summary>Creates a host using the actual test instance's configured assembly run.</summary>
    /// <remarks>Complete assembly reporting and call TUnitBDTestAssembly.Dispose after all tests finish.</remarks>
    public static BDTestScenario<TContext> For<TContext>(object testInstance) where TContext : class =>
        For<TContext>(testInstance, TUnitBDTestAssembly.GetRun(
            (testInstance ?? throw new ArgumentNullException(nameof(testInstance))).GetType().Assembly));

    /// <summary>Creates a host with a caller-owned run. Register this framework's outcomes before starting it.</summary>
    public static BDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run) where TContext : class =>
        BDTestScenario.For<TContext>(testInstance, run ?? throw new ArgumentNullException(nameof(run)));
}
