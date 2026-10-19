using BDTest.Settings;
using BDTest.Test;

namespace BDTest;

/// <summary>Supplies the NUnit scenario host owned by a test fixture.</summary>
public interface INUnitBDTest
{
    /// <summary>Returns this fixture's stable host created by NUnitBDTestScenario.For, or its NUnitBDTestBase.</summary>
    BDTestBase BDTestHost { get; }
}

/// <summary>Creates NUnit scenario hosts without requiring a fixture base class.</summary>
public static class NUnitBDTestScenario
{
    /// <summary>Creates a host using the run owned by the actual test instance's assembly.</summary>
    /// <remarks>Complete assembly reporting and call NUnitBDTestAssembly.Dispose after all tests finish.</remarks>
    public static NUnitBDTestScenario<TContext> For<TContext>(object testInstance) where TContext : class =>
        For<TContext>(testInstance, NUnitBDTestAssembly.GetRun(
            (testInstance ?? throw new ArgumentNullException(nameof(testInstance))).GetType().Assembly));

    /// <summary>Register these outcomes before starting an explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => NUnitExceptionRegistration.OutcomeMap;

    /// <summary>Creates a host for an INUnitBDTest fixture marked with BDTestFixtureAttribute.</summary>
    /// <remarks>The caller owns the run and must dispose it after all tests and teardown finish.</remarks>
    public static NUnitBDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run)
        where TContext : class => new(testInstance, run ?? throw new ArgumentNullException(nameof(run)));
}

/// <summary>An NUnit-aware core composition host, with assertion isolation for scenario retries.</summary>
public class NUnitBDTestScenario<TContext> : BDTestScenario<TContext>, INUnitBDTestLifecycle
    where TContext : class
{
    protected internal NUnitBDTestScenario(object testInstance, BDTestRun run) : base(testInstance, run)
    {
        _testInstance = testInstance;
        Lifecycle = new NUnitTestLifecycle(BeginTest, () => NUnitTestLifecycle.CompleteOnRunner(EndTestAsync));
    }

    private readonly object _testInstance;
    object INUnitBDTestLifecycle.TestInstance => _testInstance;
    internal NUnitTestLifecycle Lifecycle { get; }
    NUnitTestLifecycle INUnitBDTestLifecycle.Lifecycle => Lifecycle;

    protected override void OnScenarioSkipped(string reason) => NUnitBDTestScenario.OutcomeMap.ReportSkip(reason);

    protected override Task ExecuteScenarioStepsAsync(Func<Task> executeSteps, Action<Action> registerTerminalFailure) =>
        NUnitScenarioExecution.ExecuteAsync(executeSteps, registerTerminalFailure);
}
