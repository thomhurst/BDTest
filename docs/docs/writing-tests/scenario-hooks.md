---
title: Scenario hooks
description: Run setup and cleanup for each scenario attempt with typed context, outcome and cancellation access.
---

# Scenario hooks

For built-in adapters, configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md). Examples using `BDTestSettings` below apply only to the legacy standalone default run; use the corresponding option registrations for adapter-owned runs.


Scenario hooks surround one attempt, including its [step hooks](step-hooks.md). Use them for per-scenario data, browser traces, telemetry and cleanup that must run after failures. All adapters use the same execution path.

## Per-test hooks

Override the typed hooks on any context-based test base:

```csharp
using BDTest.NUnit;
using BDTest.Test;

public class ScenarioState
{
    public DateTime StartedAt { get; set; }
}

public abstract class InstrumentedTests : NUnitBDTestBase<ScenarioState>
{
    public override Task OnBeforeScenario(ScenarioHookContext<ScenarioState> attempt)
    {
        attempt.Context.StartedAt = DateTime.UtcNow;
        Console.WriteLine($"Starting {attempt.Scenario.GetScenarioText()}, attempt {attempt.Attempt}");
        return Task.CompletedTask;
    }

    public override Task OnAfterScenario(ScenarioHookContext<ScenarioState> attempt)
    {
        Console.WriteLine($"Finished {attempt.Scenario.Status}: {attempt.Scenario.Exception?.Message}");
        return Task.CompletedTask;
    }
}
```

`ScenarioHookContext<TContext>` provides:

- `Scenario`: the executing scenario, including text, story, tags, steps and example values. After hooks see the attempt's status and exception.
- `Context`: the same typed object as the test's `Context` when the attempt starts.
- `Attempt`: zero for the initial attempt, then one, two and so on for retries. Report `ScenarioAttempt.Number` is one-based instead.
- `CancellationToken`: a fresh token for this attempt, cancelled when a step times out, before after hooks run. This token is separate from step tokens. An ordinary exception does not cancel it. Run disposal does not cancel active attempts.

`BDTestBase` also exposes untyped `OnBeforeScenario(ScenarioHookContext)` and `OnAfterScenario(ScenarioHookContext)` hooks. The untyped context exposes `object Context`, which is null for a test without a context-based base class. Context-based bases dispatch these hooks to their typed overloads.

## Run hooks

For an [explicit run](../advanced/explicit-runs.md), implement `BDTest.Interfaces.IScenarioHook` and register it before starting the run:

```csharp
using BDTest;
using BDTest.Interfaces;
using BDTest.Test;

var options = new BDTestOptions();
options.ScenarioHooks.Add(new ScenarioLogger());
using var run = BDTestRun.Start(options);
// Override TestRun on your fixture to return run.

public sealed class ScenarioLogger : IScenarioHook
{
    public Task BeforeScenarioAsync(ScenarioHookContext attempt)
    {
        Console.WriteLine($"Starting {attempt.Scenario.GetStoryText()}");
        return Task.CompletedTask;
    }

    public Task AfterScenarioAsync(ScenarioHookContext attempt)
    {
        Console.WriteLine($"Result: {attempt.Scenario.Status}");
        return Task.CompletedTask;
    }
}
```

`Start(options)` copies the registrations. Later registration changes affect only later runs. Explicit runs do not inherit global events. Hook objects can run concurrently for different tests and must protect their shared state.

For the legacy default run, subscribe to `BDTestSettings.BeforeScenario` and `BDTestSettings.AfterScenario`. Both are `Func<ScenarioHookContext, Task>` events. Remove subscriptions with `-=` when no longer needed. Both event lists are captured at the start of each attempt; changes during an attempt apply to later attempts.

## Order and failures

BDTest creates, configures and asynchronously initializes the context before scenario hooks begin. Preparation runs inside the adapter assertion scope so a failed assertion can retry without contaminating the final runner result.

For every prepared attempt, BDTest awaits:

1. Run or global before hooks in registration order.
2. Instance `OnBeforeScenario`.
3. Scenario skip checks, then steps with their step hooks.
4. Instance `OnAfterScenario`.
5. Run or global after hooks in registration order.

Before hooks stop at the first exception. Steps remain `NotRun` after an ordinary before-hook failure. Every after hook runs once the attempt's hook context has been created, even if a before hook or another after hook fails. A context construction, configuration, initialization or retry-setup failure happens before scenario hooks begin; no scenario hooks run for that failed preparation.

An execution exception takes precedence over after-hook exceptions. Otherwise, the first after-hook exception determines the result. Hook exceptions follow the usual [exception classification](../configuration/exceptions.md) and [retry rules](retries.md). Later after hooks see a result updated by an earlier after-hook failure. Secondary failures are logged. Reports include console output from scenario hooks, including when a skip or before-hook failure prevents steps from running.

Whole-scenario skip rules still run scenario hooks, while no step hooks run for that skip. Framework skips that prevent the test method from running cannot invoke BDTest hooks. Each retry gets a fresh context and token after retry setup. Each [scenario outline](scenario-outlines.md) row runs hooks independently on each attempt; the outline container does not run hooks. Definition/factory failures before a row can execute do not run hooks.

## Timing and cleanup

Scenario and attempt timing include before and after scenario hooks. Step durations exclude scenario hooks. After hooks receive the attempt outcome before its timing record is finalized.

Scenario hooks are awaited without a timeout and do not consume the [scenario step budget](timeouts.md#hooks-and-the-scenario-budget). The hook token signals a step timeout; it does not impose a limit on hook execution. Cleanup can use its own timeout or ignore an already-cancelled token when necessary to save diagnostics. Cancellation is observable before after hooks run, but registered cancellation callbacks run in the background so they cannot block cleanup. Callback exceptions do not replace the timeout failure.

Story-level hooks are not provided. Use the test framework's class fixtures for shared story setup.
