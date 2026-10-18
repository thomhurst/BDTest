using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.Sdk;

namespace BDTest.xUnit;

[BDTestLifecycle]
public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable, IAsyncLifetime
    where TContext : class, new()
{
    private readonly string _frameworkTestId;
    private readonly StartupOutputBuffer _constructorOutput;
    private bool _executionStarted;

    protected override string BDTestExecutionId => _frameworkTestId;

    protected XUnitBDTestBase()
    {
        XUnitExceptionRegistration.EnsureRegistered();
        _frameworkTestId = TestContext.Current.Test?.UniqueID
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest adapter.");
        _constructorOutput = new StartupOutputBuffer(InitializeExecution);
        TestOutputData.PendingStartupOutput = _constructorOutput;
        Console.SetOut(TestOutputData.Instance);
    }

    // Keep lifecycle binding outside the virtual async setup method: AsyncLocal
    // changes inside a derived async override do not flow back to xUnit's caller.
    ValueTask IAsyncLifetime.InitializeAsync()
    {
        InitializeExecution();
        return InitializeAsync();
    }

    private void InitializeExecution()
    {
        if (_executionStarted) return;
        BeginTest(_frameworkTestId);
        _constructorOutput.Bind(TestRun, BDTestExecutionId);
        _executionStarted = true;
    }

    /// <summary>Runs async setup after the execution has been bound to the derived fixture's run.</summary>
    public virtual ValueTask InitializeAsync() => default;

    protected override void OnScenarioSkipped(string reason) => Assert.Skip(reason);

    /// <summary>Completes the BDTest execution when xUnit disposes the test asynchronously.</summary>
    /// <remarks>Override for asynchronous teardown and await base.DisposeAsync() in a finally block.</remarks>
    public virtual ValueTask DisposeAsync()
    {
        // Preserve synchronous teardown when a derived class reimplements IDisposable.
        ((IDisposable)this).Dispose();
        return default;
    }

    public void Dispose()
    {
        // A derived IAsyncLifetime implementation may bypass our initialization hook.
        InitializeExecution();
        _constructorOutput.Complete();
        EndTest();
        if (ReferenceEquals(TestOutputData.PendingStartupOutput, _constructorOutput))
            TestOutputData.PendingStartupOutput = null;
    }
}

// Non-generic so all context types share the same CLR-protected initialization.
internal static class XUnitExceptionRegistration
{
    static XUnitExceptionRegistration()
    {
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(SkipException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(SkipException));
        }
    }

    internal static void EnsureRegistered() { }
}
