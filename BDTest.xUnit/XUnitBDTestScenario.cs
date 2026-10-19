using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.v3;

namespace BDTest;

/// <summary>Creates framework-owned composition hosts without requiring a test base class.</summary>
public static class XUnitBDTestScenario
{
    /// <summary>Creates a host using the run owned by the actual test instance's assembly.</summary>
    /// <remarks>Complete assembly reporting and call XUnitBDTestAssembly.Dispose after all tests finish.</remarks>
    public static BDTestScenario<TContext> For<TContext>(object testInstance) where TContext : class =>
        For<TContext>(testInstance, XUnitBDTestAssembly.GetRun(
            (testInstance ?? throw new ArgumentNullException(nameof(testInstance))).GetType().Assembly));

    /// <summary>The xUnit outcome declaration. Register on options before starting the explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => XUnitExceptionRegistration.OutcomeMap;

    /// <summary>Starts a host for the current xUnit test and completes it after the runner's teardown.</summary>
    /// <remarks>
    /// Register XUnitBDTestLifecycle as an assembly fixture. Call from the test constructor,
    /// passing the actual test instance and an explicit run. The caller owns the run's lifetime.
    /// Only one composition host may be created per native test and run, even after completion.
    /// </remarks>
    public static BDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run)
        where TContext : class
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (testInstance == null) throw new ArgumentNullException(nameof(testInstance));
        var test = TestContext.Current.Test as IXunitTest
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest host.");
        var currentInstance = TestContext.Current.TestClassInstance;
        if (currentInstance != null && !ReferenceEquals(currentInstance, testInstance))
            throw new ArgumentException("Pass the running xUnit test instance.", nameof(testInstance));
        var host = new FrameworkScenario<TContext>(testInstance, run);
        var execution = new XUnitTestExecution(testInstance,
            () => host.BeginTest(test.UniqueID, test.TestMethod.Method),
            host.EndTestAsync, compositionRun: run);
        execution.Begin();
        return host;
    }

    // Resolve at execution time: factories can run before the runner installs its test token.
    private sealed class FrameworkScenario<TContext>(object testInstance, BDTestRun run)
        : BDTestScenario<TContext>(testInstance, run) where TContext : class
    {
        protected override CancellationToken FrameworkCancellationToken => TestContext.Current.CancellationToken;
    }
}
