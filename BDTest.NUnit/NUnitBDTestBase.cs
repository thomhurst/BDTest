ï»¿using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;
using System.Reflection;

namespace BDTest.NUnit;

[TestFixture, BDTestFixture]
public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, INUnitBDTest, INUnitBDTestLifecycle where TContext : class
{
    private readonly NUnitTestLifecycle _lifecycle;

    protected NUnitBDTestBase() => _lifecycle = new NUnitTestLifecycle(BeginTest, Complete);

    BDTestBase INUnitBDTest.BDTestHost => this;
    NUnitTestLifecycle INUnitBDTestLifecycle.Lifecycle => _lifecycle;

    [OneTimeSetUp]
    public static void SetupNUnitExceptions()
    {
        NUnitExceptionRegistration.EnsureRegistered();
    }

    /// <summary>The reusable outcome declaration. Register it on options for explicit runs.</summary>
    public static FrameworkOutcomeMap OutcomeMap => NUnitExceptionRegistration.OutcomeMap;

    protected override void OnScenarioSkipped(string reason) => OutcomeMap.ReportSkip(reason);

    [SetUp]
    public void Setup()
    {
        if (!_lifecycle.IsWrapped)
        {
            // Keep direct setup calls usable for existing adapter harnesses.
#pragma warning disable CS0618
            _lifecycle.Begin(NUnitTestContext.CurrentContext.Test.ID,
                NUnitTestContext.CurrentContext.CurrentRepeatCount,
                NUnitTestContext.CurrentContext.Test.Method?.MethodInfo);
#pragma warning restore CS0618
        }
    }

    // Kept callable for existing consumers; normal NUnit teardown is asynchronous.
    public void PruneContext() => EndTest();

    protected override Task ExecuteScenarioStepsAsync(Func<Task> executeSteps,
        Action<Action> registerTerminalFailure) =>
        NUnitScenarioExecution.ExecuteAsync(executeSteps, registerTerminalFailure);

    [TearDown]
    public Task BDTestNUnitTearDown() =>
        _lifecycle.IsWrapped ? Task.CompletedTask : CompleteAsync().AsTask();

    private bool HasCompletionOverride =>
        GetType().GetMethod(nameof(MarkTestAsComplete), BindingFlags.Instance | BindingFlags.NonPublic)
            ?.DeclaringType != typeof(NUnitBDTestBase<TContext>);

    private ValueTask CompleteAsync()
    {
        if (!HasCompletionOverride) return EndTestAsync();
        Complete();
        return default;
    }

    private void Complete()
    {
        // EndTest starts asynchronous disposal without the runner's synchronization
        // context, then clears capture on the caller's flow even if disposal fails.
        try
        {
            if (HasCompletionOverride)
            {
#pragma warning disable CS0618
                MarkTestAsComplete();
#pragma warning restore CS0618
            }
        }
        finally { EndTest(); }
    }

    // Retain the virtual hook so existing NUnit completion overrides still run.
    [Obsolete("Await EndTestAsync() in new adapters. Retained for NUnit teardown compatibility.")]
    protected override void MarkTestAsComplete()
    {
        PruneContext();
    }
}

// Shared by every generic context type; all registration is owned by the core.
internal static class NUnitExceptionRegistration
{
    internal static FrameworkOutcomeMap OutcomeMap { get; } = new FrameworkOutcomeMap(reason => Assert.Ignore(reason), reason => Assert.Inconclusive(reason))
        .Success<SuccessException>()
        .Skip<IgnoreException>()
        .Inconclusive<InconclusiveException>();

    internal static void EnsureRegistered() =>
        BDTestSettings.CustomExceptionSettings.RegisterFrameworkOutcomes(OutcomeMap);
}
