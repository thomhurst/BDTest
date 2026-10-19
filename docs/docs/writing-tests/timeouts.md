---
title: Timeouts and cancellation
description: Cancel scenario execution and bound steps, hooks and scenario attempts.
---

# Timeouts and cancellation

Import `BDTest` to use the timeout extensions. Async steps can accept a `CancellationToken` in every `Given`, `When`, `Then`, `And` and `But` position:

```csharp title="Illustrative fragment"
using BDTest;

var scenario = await Given(token => Orders.PrepareAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(5))
    .When(token => Api.SendRequestAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(10))
    .Then(() => Assertions.TheResponseIsValid())
    .WithScenarioTimeout(TimeSpan.FromSeconds(20))
    .BDTestAsync(cancellationToken);
```

`BDTestAsync(CancellationToken)` is available for both scenario chains and outlines. The parameterless overload and direct `await` remain supported. The effective token combines the explicit execution token, any token provided by the [adapter](../frameworks/custom-adapters.md#runner-cancellation), and active timeout limits. Even without an external cancellation source or timeout, step delegates receive a fresh cancellable token for the attempt; it remains uncancelled during normal execution. Cancellation token arguments are left out of generated step text and `[StepText]` placeholders.

| Method | Applies to |
| --- | --- |
| `WithStepTimeout(TimeSpan)` | The most recently added step, including its before and after hooks. |
| `WithScenarioTimeout(TimeSpan)` | One execution attempt, including context preparation, scenario hooks, steps and step hooks. |

The earlier applicable limit wins. Timeouts are optional. They must be positive and at most `Int32.MaxValue` milliseconds. Each permitted [retry](retries.md) receives a fresh scenario budget and new step tokens.

## When a limit expires

BDTest requests cancellation and throws a `TimeoutException` identifying the limit and its scope. The scenario is `Failed`, with `TimedOut = true`; an active step also records the timeout. A timeout during preparation or a before-scenario hook leaves unreached steps `NotRun`. Later steps do not run.

An explicit or adapter cancellation instead fails execution with an `OperationCanceledException` and does not set `TimedOut`. Cancellation before execution prevents step bodies from running. Cancellation ends retrying and stops subsequent outline rows. A user-thrown exception before cancellation or timeout keeps its usual classification; cleanup errors do not replace the primary execution failure.

## Cooperative cancellation

Pass the effective token to every cancellable operation. Cancellation cannot forcibly stop user code.

With a timeout, BDTest runs bounded operations on the thread pool so a limit can interrupt waiting for synchronous work or the synchronous prefix of an async method. Without a timeout, invocation begins directly on the caller's context. Explicit cancellation can bound waiting for yielding asynchronous work, but cannot interrupt synchronous or non-yielding code before it returns control.

After cancellation, BDTest allows each cancelled operation up to 250 milliseconds of real time to settle. If work or a cancellation callback is still running, BDTest observes its eventual failure and suppresses its late captured console and custom output, including startup and teardown output. Late completion cannot change recorded outcomes. It also disables retries for that scenario and stops remaining outline rows, so unfinished work cannot race a replacement context. Background work may still have application side effects; keep it cooperative.

## Hooks and the scenario budget

The scenario budget includes preparation performed during the attempt, before and after scenario hooks, and every step and step hook. The step budget starts before before-step hooks and includes the body and after-step hooks. See [Scenario hooks](scenario-hooks.md) and [Step hooks](step-hooks.md) for token access.

After hooks are still attempted after cancellation or timeout, with a cancelled token. Each cleanup operation has the same bounded 250 millisecond grace period. Already-cancelled cleanup runs on the thread pool so synchronous cleanup cannot block execution indefinitely. Keep cleanup brief and do not assume a fresh timeout budget. A cleanup failure is secondary when execution has already failed.

When work remains abandoned after the grace period, adapter teardown returns without waiting for it. Its context remains alive until that work and its cancellation callbacks finish; owned context disposal then runs in the background. This also applies to abandoned context creation or initialization. If the work never finishes, its context cannot safely be disposed. Background disposal cannot publish output or change the completed result, and any disposal exception is observed without replacing the original failure.

Framework setup, retry teardown/setup hooks, `OnBeforeRetry`, and context recreation before the next attempt begins are outside the scenario timeout budget. Context work before an outline row's chain exists also cannot use that chain's scenario timeout. Use an explicit execution token for outline preparation and a suitable limit for framework setup or retry hooks. Outline factories are invoked directly, so a cancellation token cannot interrupt their synchronous work.

## Runtime implementation

The modern asset uses the run's [execution clock](../advanced/explicit-runs.md#execution-clock-net-8) for timeout timers. The .NET Standard asset uses system timers. The cancellation grace period always uses real time, including when the run uses a fake clock. Both assets preserve the timeout, cancellation, retry and late-completion behavior described above.
