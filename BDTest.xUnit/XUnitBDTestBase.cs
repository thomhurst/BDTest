using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using System.Reflection;

namespace BDTest.xUnit;

[BDTestLifecycle]
public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable, IAsyncLifetime
    where TContext : class
{
    private readonly string _frameworkTestId;
    private bool _executionStarted;

    protected override string BDTestExecutionId => _frameworkTestId;

    protected XUnitBDTestBase()
    {
        XUnitExceptionRegistration.EnsureRegistered();
        _frameworkTestId = TestContext.Current.Test?.UniqueID
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest adapter.");
        CaptureStartupOutput(InitializeExecution);
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
        BeginTest(_frameworkTestId, (TestContext.Current.TestMethod as IXunitTestMethod)?.Method);
        BindStartupOutput();
        _executionStarted = true;
    }

    /// <summary>Runs async setup after the execution has been bound to the derived fixture's run.</summary>
    public virtual ValueTask InitializeAsync() => default;

    internal static void BeginPendingExecution() => BeginPendingTestExecution();

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => XUnitExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    /// <summary>Completes the BDTest execution when xUnit disposes the test asynchronously.</summary>
    /// <remarks>Override for asynchronous teardown and await base.DisposeAsync() in a finally block.</remarks>
    public virtual async ValueTask DisposeAsync()
    {
        var completionMethod = GetType().GetMethod(nameof(MarkTestAsComplete),
            BindingFlags.Instance | BindingFlags.NonPublic);
        var disposeMethod = GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
        // Legacy overrides must see their live context and output before completing.
        if (completionMethod?.DeclaringType != typeof(BDTestBase) ||
            disposeMethod.DeclaringType != typeof(XUnitBDTestBase<TContext>))
        {
            ((IDisposable)this).Dispose();
            return;
        }

        InitializeExecution();
        CompleteStartupOutput();
        await EndTestAsync();
    }

    public void Dispose()
    {
        // A derived IAsyncLifetime implementation may bypass our initialization hook.
        InitializeExecution();
        CompleteStartupOutput();
#pragma warning disable CS0618 // Preserve existing overrides of the compatibility hook.
        MarkTestAsComplete();
#pragma warning restore CS0618
    }
}

// Shared by every generic context type; all registration is owned by the core.
internal static class XUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => Assert.Skip(reason))
        .Skip<SkipException>();

    internal static void EnsureRegistered() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
