using System.Runtime.ExceptionServices;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Interfaces;
using NUnitTestContext = NUnit.Framework.TestContext;

namespace BDTest;

internal static class NUnitScenarioExecution
{
    internal static async Task ExecuteAsync(Func<Task> executeSteps,
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
}
