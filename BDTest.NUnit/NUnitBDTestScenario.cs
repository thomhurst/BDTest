using BDTest.Settings;
using BDTest.Test;

namespace BDTest.NUnit;

/// <summary>Supplies the NUnit scenario host owned by a test fixture.</summary>
public interface INUnitBDTest
{
    /// <summary>Returns this fixture's stable host created by NUnitBDTestScenario.For, or its NUnitBDTestBase.</summary>
    BDTestBase BDTestHost { get; }
}

/// <summary>Creates NUnit scenario hosts without requiring a fixture base class.</summary>
public static class NUnitBDTestScenario
{
    /// <summary>Register these outcomes before starting an explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => NUnitExceptionRegistration.OutcomeMap;

    /// <summary>Creates a host for an INUnitBDTest fixture marked with BDTestFixtureAttribute.</summary>
    /// <remarks>The caller owns the run and must dispose it after all tests and teardown finish.</remarks>
    public static NUnitBDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run)
        where TContext : class, new() => new(testInstance, run ?? throw new ArgumentNullException(nameof(run)));
}

/// <summary>An NUnit-aware core composition host, with assertion isolation for scenario retries.</summary>
public class NUnitBDTestScenario<TContext> : BDTestScenario<TContext>, INUnitBDTestLifecycle
    where TContext : class, new()
{
    protected internal NUnitBDTestScenario(object testInstance, BDTestRun run) : base(testInstance, run)
    {
        Lifecycle = new NUnitTestLifecycle(BeginTest, EndTest);
    }

    internal NUnitTestLifecycle Lifecycle { get; }
    NUnitTestLifecycle INUnitBDTestLifecycle.Lifecycle => Lifecycle;

    protected override void OnScenarioSkipped(string reason) => NUnitBDTestScenario.OutcomeMap.ReportSkip(reason);

    protected override Task ExecuteScenarioStepsAsync(Func<Task> executeSteps, Action<Action> registerTerminalFailure) =>
        NUnitScenarioExecution.ExecuteAsync(executeSteps, registerTerminalFailure);
}
