using System.Reflection;

namespace BDTest.NUnit;

/// <summary>Owns one configured BDTest run per test assembly for NUnit.</summary>
public static class NUnitBDTestAssembly
{
    private static readonly BDTestAssemblyRuns Runs = new(NUnitExceptionRegistration.OutcomeMap);

    /// <summary>Gets the assembly's shared run, configuring it once on first access.</summary>
    public static BDTestRun GetRun(Assembly assembly) => Runs.GetRun(assembly);

    /// <summary>Disposes the assembly run after all tests and report exports finish.</summary>
    /// <remarks>
    /// The test host owns this call, including between repeated runs in the same process.
    /// Existing fixtures keep their disposed run; the next lookup creates a fresh run for new fixtures.
    /// Do not call while tests, teardown, output writers or attachment readers are active.
    /// </remarks>
    public static void Dispose(Assembly assembly) => Runs.Dispose(assembly);
}
