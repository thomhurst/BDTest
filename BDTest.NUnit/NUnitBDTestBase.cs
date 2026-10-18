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
        // NUnit's replacement MethodInfoAdapter does not expose the exact reflection method.
#pragma warning disable CS0618 // Retain public Method access for overload-safe identity.
        BeginTest($"{NUnitTestContext.CurrentContext.Test.ID}_{NUnitTestContext.CurrentContext.CurrentRepeatCount}",
            NUnitTestContext.CurrentContext.Test.Method?.MethodInfo);
#pragma warning restore CS0618
    }

    // Kept callable for existing consumers; NUnit teardown uses MarkTestAsComplete.
    public void PruneContext() => EndTest();

    internal override async Task ExecuteScenarioStepsAsync(Func<Task> executeSteps,
        Action<Action> registerTerminalFailure)
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

        var failures = assertions.Where(assertion => assertion.Status == AssertionStatus.Failed ||
                                                     assertion.Status == AssertionStatus.Error).ToArray();
        if (result.ActualValue is Exception exception)
        {
            // A normal assertion exception already carries its own diagnostic.
            if (failures.Length > 1 || failures.Length == 1 && failures[0].Message != exception.Message)
            {
                registerTerminalFailure(() => throw new AggregateException(
                    "Scenario failed with NUnit assertion failures.",
                    new[] { exception }.Concat(failures.Select(failure =>
                        new AssertionException(failure.Message + Environment.NewLine + failure.StackTrace)))));
            }
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        // Also preserve failures the user caught, or deferred in an outer multiple scope.
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
