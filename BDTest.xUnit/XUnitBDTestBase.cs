using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using System.Reflection;

namespace BDTest;

[BDTestLifecycle]
public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable, IAsyncLifetime
    where TContext : class
{
    private BDTestRun? _assemblyRun;

    /// <summary>The configured run owned by this fixture's assembly, or an explicit override.</summary>
    public override BDTestRun TestRun => _assemblyRun ??= XUnitBDTestAssembly.GetRun(GetType().Assembly);

    private readonly string _frameworkTestId;
    private readonly XUnitTestExecution _execution;

    protected override string BDTestExecutionId => _frameworkTestId;

    protected XUnitBDTestBase()
    {
        _frameworkTestId = TestContext.Current.Test?.UniqueID
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest adapter.");
        _execution = new XUnitTestExecution(this,
            () =>
            {
                BeginTest(_frameworkTestId, (TestContext.Current.TestMethod as IXunitTestMethod)?.Method);
                BindStartupOutput();
            }, EndTestAsync, CompleteStartupOutput);
        CaptureStartupOutput(InitializeExecution);
    }

    // Keep lifecycle binding outside the virtual async setup method: AsyncLocal
    // changes inside a derived async override do not flow back to xUnit's caller.
    ValueTask IAsyncLifetime.InitializeAsync()
    {
        InitializeExecution();
        return InitializeAsync();
    }

    private void InitializeExecution() => _execution.Begin();

    internal static void BeginPendingExecution() => BeginPendingTestExecution();

    /// <summary>Runs async setup after the execution has been bound to the derived fixture's run.</summary>
    public virtual ValueTask InitializeAsync() => default;

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => XUnitExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    /// <summary>Completes the BDTest execution when xUnit disposes the test asynchronously.</summary>
    /// <remarks>Override for asynchronous teardown and await base.DisposeAsync() in a finally block.</remarks>
    public virtual ValueTask DisposeAsync()
    {
        var completionMethod = GetType().GetMethod(nameof(MarkTestAsComplete),
            BindingFlags.Instance | BindingFlags.NonPublic);
        var disposeMethod = GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
        // Legacy overrides must see their live context and output before completing.
        if (completionMethod?.DeclaringType != typeof(BDTestBase) ||
            disposeMethod.DeclaringType != typeof(XUnitBDTestBase<TContext>))
        {
            ((IDisposable)this).Dispose();
            return default;
        }

        return _execution.CompleteAsync();
    }

    public void Dispose()
    {
        // Legacy overrides still run while context/output are live. The shared
        // completion path makes runner notifications and repeated disposal safe.
        _execution.CompleteAsync(() =>
        {
#pragma warning disable CS0618 // Preserve existing overrides of the compatibility hook.
            try { MarkTestAsComplete(); }
            finally { EndTest(); }
#pragma warning restore CS0618
            return default;
        }).GetAwaiter().GetResult();
    }
}

// Immutable declaration shared by every context type; registrations belong to each run.
internal static class XUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => Assert.Skip(reason))
        .Skip<SkipException>();
}
