---
title: Scenarios and steps
description: Build Given / When / Then chains, the rules for which steps can follow which, and how a scenario executes.
---

# Scenarios and steps

A scenario is a chain of steps that starts with `Given` or `When` and ends with `Then`. You run it by awaiting the chain or by calling `BDTestAsync()`.

```csharp
await Given(() => Account.CreateAnAccount(100m))
    .And(() => Account.AddPayee("Alice"))
    .When(() => Account.Transfer(25m, "Alice"))
    .Then(() => Assertions.TheBalanceIs(75m))
    .And(() => Assertions.AlicesBalanceIs(25m))
    .BDTestAsync();
```

Each step is a lambda that calls one method. BDTest reads the lambda to produce the [step text](step-text.md), then runs it.

## Two ways to start

**Inherit a base class** (recommended). Use a [framework adapter](../frameworks/overview.md) such as `TUnitBDTestBase<TContext>`, or `BDTestBase` with no context:

```csharp
public class MyTests : BDTestBase
{
    [Test]
    public async Task Test1()
    {
        await Given(() => Action1())
            .When(() => Action2())
            .Then(() => Action3())
            .BDTestAsync();
    }
}
```

**Use a builder object.** `BDTestBuilder` is a `BDTestBase` with its own execution ID. Create a new one for each test:

```csharp
public class MyTests
{
    [Test]
    public async Task Test1()
    {
        await new BDTestBuilder()
            .Given(() => Action1())
            .When(() => Action2())
            .Then(() => Action3())
            .BDTestAsync();
    }
}
```

## Chain rules

BDTest enforces a clear structure through its types. The compiler rejects a chain that breaks these rules.

| After | You can call |
| --- | --- |
| *start* | `Given`, `When` |
| `Given` | `And`, `But`, `When` |
| `Given(...).And` / `.But` | `And`, `But`, `When` |
| `When` | `But`, `Then` |
| `Then` | `And`, `But`, `BDTestAsync()`, `await` |
| `Then(...).And` / `.But` | `And`, `But`, `BDTestAsync()`, `await` |

- A scenario must contain a `When` and a `Then`. `Given` is optional: use it when the test needs setup to reach the required state.
- There is no `And` after `When`. A scenario tests one action.

### But steps

Use `.But(...)` to express a contrast. It keeps the current phase of the chain and renders the `But` keyword in step text and reports:

```csharp
await Given(() => AccountExists())
    .But(() => AccountIsLocked())
    .When(() => SignInAsync())
    .But(() => RetrySignInAsync())
    .Then(() => AccessIsDenied())
    .But(() => AccountStillExists())
    .BDTestAsync();
```

A `But` step in the `When` phase is skipped when [`Debug.ShouldSkipWhenStep`](../configuration/settings.md#debugging-tests) is on.

## Step overloads

Every `Given`, `When`, `Then`, `And` and `But` accepts three kinds of lambda:

| Overload | Example |
| --- | --- |
| `Expression<Action>` | `Given(() => CreateUser())` |
| `Expression<Func<Task>>` | `When(() => SubmitAsync())` |
| `Expression<Func<CancellationToken, Task>>` | `When(token => SubmitAsync(token))` |

The token overload is for [timeouts](timeouts.md). See [Asynchronous steps](async-steps.md) for `ValueTask` and other awaitables.

## Running the scenario

`BDTestAsync()` returns `Task<Scenario>`. The builder is also awaitable, so these are equivalent:

```csharp
Scenario scenario = await Given(...).When(...).Then(...).BDTestAsync();
Scenario scenario = await Given(...).When(...).Then(...);
```

The returned [`Scenario`](../advanced/utilities.md#scenario-and-step-model) holds the status, steps, timings, output and retry history.

When a step throws:

1. That step is marked with a status derived from the exception (usually `Failed`, see [exception classification](../configuration/exceptions.md)).
2. Steps after it are not executed and stay `NotRun`.
3. The scenario takes the step's status, and BDTest rethrows the exception so your test runner reports the failure.

### A chain runs once

A runnable builder supports one execution. Calling `BDTestAsync()` or awaiting the same builder again, including while it runs or after it fails, throws `BDTest.Exceptions.AlreadyExecutedException` before any steps run again.

Each continuation (for example `Then(...).And(...)`) creates an independent builder with its own execution limit, step state and report entry. Create a new chain to run another scenario. [Retries](retries.md) happen inside the original execution and keep one report entry.

## Overriding scenario text

`[ScenarioText]` on the test method is the usual way to name a scenario (see [Stories and scenarios](stories-and-scenarios.md)). You can also set it on the chain:

```csharp
await When(() => Action())
    .Then(() => Check())
    .WithScenarioText("Checking works")
    .BDTestAsync();
```

## Tests that are not run

BDTest records every chain it builds. If a chain is built but never executed, it appears in reports as a **not run** test (on the report server's *Warnings* page). This catches tests that forgot to `await` the chain.
