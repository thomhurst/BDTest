using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.v3;

namespace BDTest.xUnit;

/// <summary>Creates framework-owned composition hosts without requiring a test base class.</summary>
public static class XUnitBDTestScenario
{
    /// <summary>The xUnit outcome declaration. Register on options before starting the explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => XUnitExceptionRegistration.OutcomeMap;

    /// <summary>Starts a host for the current xUnit test and completes it after the runner's teardown.</summary>
    /// <remarks>
    /// Register XUnitBDTestLifecycle as an assembly fixture. Call from the test constructor,
    /// passing the actual test instance and an explicit run. The caller owns the run's lifetime.
    /// Only one composition host may be created per native test and run, even after completion.
    /// </remarks>
    public static BDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run)
        where TContext : class, new()
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        var test = TestContext.Current.Test as IXunitTest
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest host.");
        var host = BDTestScenario.For<TContext>(testInstance, run);
        var execution = new XUnitTestExecution(testInstance,
            () => host.BeginTest(test.UniqueID, test.TestMethod.Method),
            host.EndTestAsync, compositionRun: run);
        execution.Begin();
        return host;
    }
}
