using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;
using NUnitTestContext = NUnit.Framework.TestContext;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Constraints;
using System.Runtime.ExceptionServices;

namespace BDTest.NUnit;

[TestFixture]
public abstract class NUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext> where TContext : class, new()
{
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
        BeginTest($"{NUnitTestContext.CurrentContext.Test.ID}_{NUnitTestContext.CurrentContext.CurrentRepeatCount}");
    }

    // Kept callable for existing consumers; NUnit teardown uses MarkTestAsComplete.
    public void PruneContext() => EndTest();

    internal override async Task ExecuteScenarioStepsAsync(Func<Task> executeSteps)
    {
        AssertionResult[] assertions = Array.Empty<AssertionResult>();
        // NUnit's public exception constraint isolates assertion state while preserving
        // the original exception. Failed attempts must not modify the runner result.
        var result = await new ThrowsNothingConstraint().ApplyToAsync(async () =>
        {
            try
            {
                await executeSteps();
                return true;
            }
            finally
            {
                assertions = NUnitTestContext.CurrentContext.Result.Assertions.ToArray();
            }
        });

        // Warnings are non-fatal and must remain visible in the runner.
        foreach (var warning in assertions.Where(assertion => assertion.Status == AssertionStatus.Warning))
        {
            Assert.Warn(warning.Message);
        }

        if (result.ActualValue is Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        // Also preserve failures the user caught, or deferred in an outer multiple scope.
        var failures = assertions.Where(assertion => assertion.Status == AssertionStatus.Failed ||
                                                     assertion.Status == AssertionStatus.Error).ToArray();
        if (failures.Length > 0)
        {
            throw new AssertionException(string.Join(Environment.NewLine, failures.Select(failure => failure.Message)));
        }
    }

    // Retain the virtual hook so existing NUnit completion overrides still run.
    [TearDown]
    [Obsolete("Call EndTest() in new adapters. Retained for NUnit teardown compatibility.")]
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
