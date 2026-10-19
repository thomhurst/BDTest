---
title: Scenario hooks
description: Run setup and cleanup for each scenario attempt with typed context, outcome and cancellation access.
---

# Scenario hooks

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


Scenario hooks surround one attempt, including its [step hooks](step-hooks.md). Use them for per-scenario data, browser traces, telemetry and cleanup that must run after failures. All adapters use the same execution path.

## Per-test hooks

Override the typed hooks on any context-based test base:

```csharp title="Illustrative fragment"
using BDTest;

public class ScenarioState
{
    public DateTime StartedAt { get; set; }
}

public abstract class InstrumentedTests : NUnitBDTestBase<ScenarioState>
{
    public override Task OnBeforeScenario(ScenarioHookContext<ScenarioState> attempt)
    {
        attempt.Context.StartedAt = DateTime.UtcNow;
        Console.WriteLine($"Starting {attempt.Scenario.ScenarioText}, attempt {attempt.Attempt}");
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

- `Scenario`: a fresh immutable `BDTest.Contracts.ScenarioResult` at each hook boundary, including text, story, tags, steps and example values. Before hooks see `Pending` and unrun steps; after hooks see the attempt outcome before timing and attempt history are finalized. Retained snapshots never change. The typed `Context` remains available for setup and cleanup.
- `Context`: the same typed object as the test's `Context` when the attempt starts. All hooks for that attempt retain this object even if a hook or step calls `OverwriteContext` or `RemoveContext`; refreshing the result snapshot does not create a new context. Context replacement/removal still follows its normal disposal rules.
- `Attempt`: zero for the initial attempt, then one, two and so on for retries. Report `ScenarioAttemptResult.Number` is one-based instead.
- `CancellationToken`: the attempt token, linked to the explicit execution token, the adapter token and scenario timeout. A step timeout also cancels it before cleanup. An ordinary exception does not cancel it. Run disposal does not cancel active attempts.

`BDTestBase` also exposes untyped `OnBeforeScenario(ScenarioHookContext)` and `OnAfterScenario(ScenarioHookContext)` hooks. The untyped context exposes `object Context`, which is null for a test without a context-based base class. Context-based bases dispatch these hooks to their typed overloads.

## Run hooks

For an [explicit run](../advanced/explicit-runs.md), implement `BDTest.Interfaces.IScenarioHook` and register it before starting the run:

```csharp title="Illustrative fragment"
using BDTest;
using BDTest.Interfaces;

var options = new BDTestOptions();
options.ScenarioHooks.Add(new ScenarioLogger());
using var run = BDTestRun.Start(options);
// Override TestRun on your fixture to return run.

public sealed class ScenarioLogger : IScenarioHook
{
    public Task BeforeScenarioAsync(ScenarioHookContext attempt)
    {
        Console.WriteLine($"Starting {attempt.Scenario.StoryText}");
        return Task.CompletedTask;
    }

    public Task AfterScenarioAsync(ScenarioHookContext attempt)
    {
        Console.WriteLine($"Result: {attempt.Scenario.Status}");
        return Task.CompletedTask;
    }
}
```

`Start(options)` copies the registrations. Later registration changes affect only later runs. Hook objects can run concurrently for different tests and must protect their shared state.


## Order and failures

BDTest creates, configures and asynchronously initializes the context before scenario hooks begin. Preparation runs inside the adapter assertion scope so a failed assertion can retry without contaminating the final runner result.

For every prepared attempt, BDTest awaits:

1. Run before hooks in registration order.
2. Instance `OnBeforeScenario`.
3. Steps with their step hooks, unless the scenario skip decision prevents them.
4. Instance `OnAfterScenario`.
5. Run after hooks in registration order.

Before hooks stop at the first exception. Steps remain `NotRun` after an ordinary before-hook failure. Every after hook is attempted once the attempt's hook context has been created, even if a before hook or another after hook fails or is cancelled. A context construction, configuration, initialization or retry-setup failure happens before scenario hooks begin; no scenario hooks run for that failed preparation.

An execution exception takes precedence over after-hook exceptions. Otherwise, the first after-hook exception determines the result. Hook exceptions follow the usual [exception classification](../configuration/exceptions.md) and [retry rules](retries.md). Later after hooks see a result updated by an earlier after-hook failure. Secondary failures are logged. Reports include console output from scenario hooks, including when a skip or before-hook failure prevents steps from running.

Whole-scenario skip rules are evaluated once before initial asynchronous context initialization and hooks (after outline factory construction). A throwing predicate fails without hooks or retries. Matching skip rules still run scenario hooks, while no step hooks run for that skip. Framework skips that prevent the test method from running cannot invoke BDTest hooks. Each retry gets a fresh context and token after retry setup. Each [scenario outline](scenario-outlines.md) row runs hooks independently on each attempt; the outline container does not run hooks. Definition/factory failures before a row can execute do not run hooks.

## Timing and cleanup

Scenario and attempt timing include before and after scenario hooks. Step durations exclude scenario hooks. After hooks receive the attempt outcome before its timing record is finalized.

The [scenario budget](timeouts.md#hooks-and-the-scenario-budget) covers context preparation during the attempt and all scenario hooks. After hooks are attempted with a cancelled token after cancellation or timeout, and each has a 250 millisecond real-time grace period. Pass `attempt.CancellationToken` to cancellable work. Cancellation callbacks cannot hold up cleanup indefinitely, and callback or cleanup failures do not replace the primary failure. If work or a callback remains unfinished after its grace period, retries and remaining outline rows stop to protect context lifetime. Late captured output is suppressed. Existing `IScenarioHook` and typed hook signatures are unchanged.

Story-level hooks are not provided. Use the test framework's class fixtures for shared story setup.
