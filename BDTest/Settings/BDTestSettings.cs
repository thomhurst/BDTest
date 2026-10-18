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
        
    [Obsolete]
    public static Dictionary<string, string> CustomProperties { get; } = new();
        
    public static DebugSettings Debug { get; } = new();

    public static List<object> CustomStringConverters { get; } = new();

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
}
