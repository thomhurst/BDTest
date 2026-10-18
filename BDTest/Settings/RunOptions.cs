using BDTest.Engine;
using BDTest.Settings.Retry;
using BDTest.Settings.Skip;
using BDTest.Test;

namespace BDTest.Settings;

// Explicit runs copy registrations once. Only the legacy default run reads live global settings.
internal sealed class RunOptions
{
    internal static RunOptions Legacy { get; } = new();
    private readonly bool _legacy;
    private readonly Type[] _success = Array.Empty<Type>();
    private readonly Type[] _skip = Array.Empty<Type>();
    private readonly Type[] _inconclusive = Array.Empty<Type>();
    private readonly RetryTestRule[] _retryRules = Array.Empty<RetryTestRule>();
    private readonly Func<Scenario, bool>[] _skipTestRules = Array.Empty<Func<Scenario, bool>>();
    private readonly SkipStepRule<object>[] _skipStepRules = Array.Empty<SkipStepRule<object>>();
    private readonly bool _skipWhen;
    private readonly Func<Step, Task>? _beforeStep;
    private readonly Func<Step, Task>? _afterStep;
    private readonly Func<ScenarioHookContext, Task>? _beforeScenario;
    private readonly Func<ScenarioHookContext, Task>? _afterScenario;

    private RunOptions() => _legacy = true;

    internal RunOptions(BDTestOptions options)
    {
#if NET8_0_OR_GREATER
        Clock = new ExecutionClock(options.TimeProvider ?? throw new ArgumentException("TimeProvider cannot be null.", nameof(options)));
#endif
        ContextFactory = options.ContextFactory;
        Services = options.Services;
        FrameworkOutcomes = options.FrameworkOutcomes;
        _success = options.SuccessExceptionTypes.ToArray();
        _skip = options.SkipExceptionTypes.ToArray();
        _inconclusive = options.InconclusiveExceptionTypes.ToArray();
        _retryRules = options.RetryTestRules.Rules.ToArray();
        _skipTestRules = options.SkipTestRules.Rules.ToArray();
        _skipStepRules = options.SkipStepRules.Rules.ToArray();
        _skipWhen = options.Debug.ShouldSkipWhenStep;
        Converters = options.Converters.ToArray();
        foreach (var hook in options.StepHooks)
        {
            if (hook == null) throw new ArgumentException("Step hooks cannot contain null.", nameof(options));
            _beforeStep += hook.BeforeStepAsync;
            _afterStep += hook.AfterStepAsync;
        }
        foreach (var hook in options.ScenarioHooks)
        {
            if (hook == null) throw new ArgumentException("Scenario hooks cannot contain null.", nameof(options));
            _beforeScenario += hook.BeforeScenarioAsync;
            _afterScenario += hook.AfterScenarioAsync;
        }
    }

    internal ExecutionClock Clock { get; } = ExecutionClock.System;

    internal FrameworkOutcomeMap? FrameworkOutcomes { get; }
    internal IBDTestContextFactory? ContextFactory { get; }
    internal IServiceProvider? Services { get; }

    internal IEnumerable<Type> SuccessExceptionTypes => _legacy ? BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes : _success;
    internal IEnumerable<Type> SkipExceptionTypes => _legacy ? BDTestSettings.CustomExceptionSettings.SkipExceptionTypes : _skip;
    internal IEnumerable<Type> InconclusiveExceptionTypes => _legacy ? BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes : _inconclusive;
    internal IReadOnlyList<RetryTestRule> RetryRules => _legacy ? BDTestSettings.GlobalRetryTestRules.Rules : _retryRules;
    internal IReadOnlyList<Func<Scenario, bool>> SkipTestRules => _legacy ? BDTestSettings.GlobalSkipTestRules.Rules : _skipTestRules;
    internal IReadOnlyList<SkipStepRule<object>> SkipStepRules => _legacy ? BDTestSettings.GlobalSkipStepRules.Rules : _skipStepRules;
    internal bool ShouldSkipWhenStep => _legacy ? BDTestSettings.Debug.ShouldSkipWhenStep : _skipWhen;
    internal Func<Step, Task>? BeforeStep => _legacy ? BDTestSettings.BeforeStepHooks : _beforeStep;
    internal Func<Step, Task>? AfterStep => _legacy ? BDTestSettings.AfterStepHooks : _afterStep;
    internal Func<ScenarioHookContext, Task>? BeforeScenario => _legacy ? BDTestSettings.BeforeScenarioHooks : _beforeScenario;
    internal Func<ScenarioHookContext, Task>? AfterScenario => _legacy ? BDTestSettings.AfterScenarioHooks : _afterScenario;
    // Null selects legacy converters in the standalone StepTextHelper API.
    internal IReadOnlyList<StepTextConverterRegistration>? Converters { get; }
}
