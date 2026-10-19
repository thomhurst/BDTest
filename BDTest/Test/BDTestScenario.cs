using System.Reflection;

namespace BDTest.Test;

/// <summary>Creates scenario hosts for test classes that do not inherit a BDTest base class.</summary>
public static class BDTestScenario
{
    /// <summary>Creates a host whose metadata and retry hooks belong to the supplied test instance.</summary>
    /// <remarks>
    /// The caller owns the supplied run's lifetime.
    /// Call BeginTest synchronously before asynchronous setup, and EndTest in teardown.
    /// </remarks>
    public static BDTestScenario<TContext> For<TContext>(object testInstance, BDTestRun run)
        where TContext : class => new(testInstance, run);
}

/// <summary>Provides context, scenario builders and output helpers through composition.</summary>
/// <remarks>
/// Uses the same lifecycle and context implementation as the inheritance API. Reuse a host only
/// with distinct framework execution IDs, and complete every execution with EndTest. Framework
/// integrations must preserve execution flow identity across setup, body and teardown.
/// </remarks>
public class BDTestScenario<TContext> : AbstractContextBDTestBase<TContext> where TContext : class
{
    /// <summary>Creates a specialized host for the supplied test instance and run.</summary>
    protected internal BDTestScenario(object testInstance, BDTestRun run)
    {
        TestInstance = testInstance ?? throw new ArgumentNullException(nameof(testInstance));
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        TestRun = run;
    }

    internal override object TestInstance { get; }

    /// <summary>Gets the run selected when the host was created. The host does not dispose it.</summary>
    public override BDTestRun TestRun { get; }

    /// <summary>Starts capture and context ownership for a unique framework test execution.</summary>
    /// <remarks>Call synchronously before asynchronous setup. IDs must distinguish data rows and repeats.</remarks>
    public new void BeginTest(string frameworkTestId) => base.BeginTest(frameworkTestId);

    /// <summary>Starts an execution with the owner's exact test method supplied by the runner.</summary>
    public new void BeginTest(string frameworkTestId, MethodInfo? testMethod) => base.BeginTest(frameworkTestId, testMethod);

    /// <summary>Completes this execution and clears its context and output state. Safe to repeat.</summary>
    public new void EndTest() => base.EndTest();

    /// <summary>Awaits owned context disposal and completes this execution. Safe to repeat.</summary>
    public new ValueTask EndTestAsync() => base.EndTestAsync();
}
