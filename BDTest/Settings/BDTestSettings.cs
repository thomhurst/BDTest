using BDTest.Interfaces;
using BDTest.Settings.Retry;
using BDTest.Settings.Skip;
using BDTest.Test;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace BDTest.Settings;

public static class BDTestSettings
{
    [Obsolete("BDTestSettings.Environment is obsolete. This should instead be passed in via BDTestRunDescriptor")]
    public static string Environment { get; set; }
        
    [Obsolete("BDTestSettings.Tag is obsolete. This should instead be passed in via BDTestRunDescriptor")]
    public static string Tag { get; set; }
        
    [Obsolete("BDTestSettings.BranchName is obsolete. This should instead be passed in via BDTestRunDescriptor")]
    public static string BranchName { get; set; }

    // Reports must still honor values set through the shipped obsolete settings.
#pragma warning disable CS0618
    internal static string LegacyEnvironment => Environment;
    internal static string LegacyTag => Tag;
    internal static string LegacyBranchName => BranchName;
#pragma warning restore CS0618
        
    [Obsolete]
    public static Dictionary<string, string> CustomProperties { get; } = new();
        
    public static DebugSettings Debug { get; } = new();

    /// <summary>Gets the legacy custom string converter list in registration order.</summary>
    /// <remarks>
    /// Prefer RegisterCustomStringConverter and RemoveCustomStringConverter during concurrent use.
    /// Direct list access is supported during setup before any concurrent access, or while holding
    /// lock (CustomStringConverters) for every read and mutation. Unsynchronized concurrent mutations
    /// are not supported. Converter implementations must provide their own thread safety.
    /// </remarks>
    public static List<object> CustomStringConverters { get; } = new();

    /// <summary>Appends a converter safely during concurrent step text generation.</summary>
    /// <remarks>
    /// Each argument is converted using a stable snapshot in registration order; the first matching
    /// converter wins. Registrations after a snapshot is taken apply to subsequent argument conversions.
    /// Converter callbacks run outside the registration lock and may run concurrently.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The converter is null.</exception>
    public static void RegisterCustomStringConverter<T>(IStepTextStringConverter<T> converter)
    {
        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        lock (CustomStringConverters)
        {
            CustomStringConverters.Add(converter);
        }
    }

    /// <summary>Removes the first equal converter safely during concurrent step text generation.</summary>
    /// <returns>True if a registration was removed; otherwise, false.</returns>
    /// <remarks>
    /// Removal does not cancel conversions using an earlier snapshot. Do not dispose converter
    /// resources until those conversions finish. Duplicate registrations are removed one at a time.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The converter is null.</exception>
    public static bool RemoveCustomStringConverter<T>(IStepTextStringConverter<T> converter)
    {
        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        lock (CustomStringConverters)
        {
            return CustomStringConverters.Remove(converter);
        }
    }

    internal static object[] GetCustomStringConvertersSnapshot()
    {
        lock (CustomStringConverters)
        {
            return CustomStringConverters.ToArray();
        }
    }

    public static CustomExceptionSettings CustomExceptionSettings { get; } = new();
        
    public static SkipStepRules GlobalSkipStepRules { get; } = new();
    public static RetryTestRules GlobalRetryTestRules { get; } = new();
    public static SkipTestRules GlobalSkipTestRules { get; } = new();

    /// <summary>Runs before each reached step, before the test instance's OnBeforeStep hook.</summary>
    /// <remarks>Handlers are awaited sequentially in registration order. Unsubscribe to remove a handler.</remarks>
    public static event Func<Step, Task> BeforeStep;

    /// <summary>Runs after each reached step, after the test instance's OnAfterStep hook.</summary>
    /// <remarks>
    /// Handlers receive the step's status, exception, output and timing, and are awaited sequentially
    /// in registration order. All after hooks run even if another hook fails. Unsubscribe to remove a handler.
    /// </remarks>
    public static event Func<Step, Task> AfterStep;

    internal static Func<Step, Task> BeforeStepHooks => BeforeStep;
    internal static Func<Step, Task> AfterStepHooks => AfterStep;

    /// <summary>Runs before the instance scenario hook on each attempt in the legacy default run.</summary>
    public static event Func<ScenarioHookContext, Task> BeforeScenario;

    /// <summary>Runs after the instance scenario hook on each attempt in the legacy default run.</summary>
    /// <remarks>All after handlers are awaited, even when an earlier hook fails.</remarks>
    public static event Func<ScenarioHookContext, Task> AfterScenario;

    internal static Func<ScenarioHookContext, Task> BeforeScenarioHooks => BeforeScenario;
    internal static Func<ScenarioHookContext, Task> AfterScenarioHooks => AfterScenario;
}
