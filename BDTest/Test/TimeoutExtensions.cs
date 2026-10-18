using BDTest.Test.Steps;

namespace BDTest.Test;

/// <summary>Configures optional execution limits while retaining the concrete fluent builder type.</summary>
public static class TimeoutExtensions
{
    /// <summary>Sets the timeout for the most recently added step.</summary>
    /// <remarks>Timeouts fail the step and request cooperative cancellation. Work that ignores cancellation may continue.</remarks>
    public static T WithStepTimeout<T>(this T builder, TimeSpan timeout) where T : StepBuilder
    {
        Validate(builder, timeout);
        builder.ExistingSteps.Last().Timeout = timeout;
        return builder;
    }

    /// <summary>Sets a shared timeout budget for all steps in each scenario attempt.</summary>
    /// <remarks>The budget restarts on retry and excludes retry hooks and report formatting.</remarks>
    public static T WithScenarioTimeout<T>(this T builder, TimeSpan timeout) where T : StepBuilder
    {
        Validate(builder, timeout);
        builder.ScenarioTimeout = timeout;
        return builder;
    }

    private static void Validate(StepBuilder builder, TimeSpan timeout)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive and at most Int32.MaxValue milliseconds.");
        }
    }
}
