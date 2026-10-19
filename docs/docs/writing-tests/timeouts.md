---
title: Timeouts
description: Bound individual steps and whole scenarios with WithStepTimeout and WithScenarioTimeout.
---

# Timeouts

Import `BDTest` to use the timeout extensions. Async steps can accept a `CancellationToken` in every `Given`, `When`, `Then`, `And` and `But` position:

```csharp title="Illustrative fragment"
using BDTest;

var scenario = await Given(token => Orders.PrepareAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(5))
    .When(token => Api.SendRequestAsync(token))
    .WithStepTimeout(TimeSpan.FromSeconds(10))
    .Then(() => Assertions.TheResponseIsValid())
    .WithScenarioTimeout(TimeSpan.FromSeconds(20))
    .BDTestAsync();
```

| Method | Applies to |
| --- | --- |
| `WithStepTimeout(TimeSpan)` | The most recently added step. |
| `WithScenarioTimeout(TimeSpan)` | A shared budget for all steps in each execution attempt. |

- The earlier of the step limit and the remaining scenario budget wins.
- Timeouts are optional. They must be positive and at most `Int32.MaxValue` milliseconds.
- Without a timeout, execution is unchanged and token-aware steps receive `CancellationToken.None`.
- Cancellation token arguments are left out of generated step text and `[StepText]` placeholders.

## When a limit expires

BDTest requests cancellation and throws a `TimeoutException` that names the limit, its scope and the active step.

- The step and scenario are `Failed`, with `TimedOut = true`. The step exception and the console summary identify the timeout.
- Later steps do not run.
- User exceptions and cancellations raised before the limit keep their usual classification.
- Each [retry](retries.md) starts a fresh scenario budget and gets new step tokens.

## Cooperative cancellation

Timed steps run on the thread pool so the limit also bounds synchronous work and the synchronous part of async methods. Cancellation is cooperative:

- Synchronous work, or async work that ignores the token, may continue after the timeout, including during a retry.
- BDTest observes the task's eventual failure. A late completion cannot change the recorded step status.

Pass the token to every cancellable operation. Avoid retries when unfinished work could affect another attempt.

## Hooks and the scenario budget

- The scenario budget covers elapsed time across steps, including [step hooks](step-hooks.md).
- Step hooks are awaited outside the execution limits. Time spent in before-step hooks reduces the remaining scenario budget when the step body starts.
- If after-step hooks use up the scenario budget, the active step fails once those hooks finish, including on the final step.
- [Scenario hooks](scenario-hooks.md) do not consume the step budget. Their attempt token is cancelled when a step times out, before scenario cleanup runs. Cleanup is awaited without a timeout.
- Retry hooks, before-scenario hooks and initial report formatting run before the scenario budget starts.

## Runtime implementation

The .NET 8 asset uses `Task.WaitAsync` with the run's [execution clock](../advanced/explicit-runs.md#execution-clock-net-8). The .NET Standard asset retains the cancellable delay implementation. Both preserve the timeout, cancellation, retry, and late-completion behavior described above.
