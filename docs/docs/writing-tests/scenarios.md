---
title: Scenarios and steps
description: Build Given / When / Then chains, the rules for which steps can follow which, and how a scenario executes.
---

# Scenarios and steps

A BDD scenario is a chain of steps that starts with `Given` or `When` and ends with `Then`. You run it by awaiting the chain or by calling `BDTestAsync()`.

```csharp title="Illustrative fragment"
await Given(() => Account.CreateAnAccount(100m))
    .And(() => Account.AddPayee("Alice"))
    .When(() => Account.Transfer(25m, "Alice"))
    .Then(() => Assertions.TheBalanceIs(75m))
    .And(() => Assertions.AlicesBalanceIs(25m))
    .BDTestAsync();
```

Each step is a delegate, usually a lambda that calls one method. BDTest runs the delegate as written. With the optional [source generator](../advanced/source-generator.md), it also knows which method the lambda calls, so it can build readable [step text](step-text.md) from `[StepText]` or the method name.

## Two ways to start

**Inherit a base class** (recommended). Use a [framework adapter](../frameworks/overview.md) such as `TUnitBDTestBase<TContext>`. A standalone `BDTestBase` has no runner method identity; it uses caller text and does not discover method attributes:

```csharp title="Illustrative fragment"
public class MyTests : TUnitBDTestBase<object>
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

```csharp title="Illustrative fragment"
public class MyTests
{
    [Test]
    public async Task Test1()
    {
        using var run = BDTestRun.Start();
        await new BDTestBuilder(run)
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
| `When` | `And`, `But`, `Then` |
| `When(...).And` / `.But` | `And`, `But`, `Then` |
| `Then` | `And`, `But`, `BDTestAsync()`, `await` |
| `Then(...).And` / `.But` | `And`, `But`, `BDTestAsync()`, `await` |

- A scenario must contain a `When` and a `Then`. `Given` is optional: use it when the test needs setup to reach the required state.
- Use `And` after `When` for additional actions in the same scenario. Each action runs in order.

### And steps in the When phase

Use `.And(...)` after `When` to add another action. It remains in the `When` phase and renders as `And` in step text and reports:

```csharp title="Illustrative fragment"
await Given(() => AccountExists())
    .When(() => SignInAsync())
    .And(() => UpdateProfileAsync())
    .Then(() => ProfileIsUpdated())
    .BDTestAsync();
```

`And` supports all the delegate overloads below, generated step metadata, lazy [`WithArgs`](step-text.md#withargs) and `WithStepText` overrides. You can continue with another `And`, a `But` or `Then`. Two continuations from the same `When` produce independent scenarios.

`And` and `But` steps in the `When` phase are skipped when [`Debug.ShouldSkipWhenStep`](../configuration/settings.md#debugging-tests) is on. Explicit runs use the debug options frozen when the run starts.

### But steps

Use `.But(...)` to express a contrast. It keeps the current phase of the chain and renders the `But` keyword in step text and reports:

```csharp title="Illustrative fragment"
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

Every `Given`, `When`, `Then`, `And` and `But` accepts these delegates:

| Overload | Example |
| --- | --- |
| `Action` | `Given(() => CreateUser())` |
| `Func<Task>` (including `Task<T>`) | `When(() => SubmitAsync())` |
| `Func<ValueTask>`, `Func<ValueTask<T>>` | `When(() => SaveValueTaskAsync())` |
| `Func<CancellationToken, Task>` | `When(token => SubmitAsync(token))` |
| `Func<CancellationToken, ValueTask>`, `Func<CancellationToken, ValueTask<T>>` | `When(token => SaveAsync(token))` |

Lambdas can have statement bodies and `await`. Method groups work too: `Given(CreateUser)`. The token overloads observe [execution cancellation and timeouts](timeouts.md). See [Asynchronous steps](async-steps.md) for the rules.

Delegate builders need a C# 13 or newer compiler (.NET SDK 9 or newer), so overload priority picks `Func<Task>` for ordinary async lambdas.

## Running the scenario

`BDTestAsync()` returns `Task<BDTest.Contracts.ScenarioResult>`. The builder is also awaitable, so these are equivalent ways to run a new chain:

```csharp sample="NUnit/ScenarioResults.cs" title="Compiled example"
using BDTest;
using BDTest.Contracts;

public static class ScenarioResultsExample
{
    public static async Task RunAsync(BDTestRun run)
    {
        ScenarioResult explicitResult = await new BDTestBuilder(run)
            .When(() => Console.WriteLine("Act"))
            .Then(() => Console.WriteLine("Check"))
            .BDTestAsync();
        ScenarioResult awaitedResult = await new BDTestBuilder(run)
            .When(() => Console.WriteLine("Act"))
            .Then(() => Console.WriteLine("Check"));
        Console.WriteLine($"{explicitResult.Status}, {awaitedResult.Status}");
    }
}
```

The returned [`ScenarioResult`](../advanced/utilities.md#scenario-and-step-model) is an immutable snapshot of the status, steps, timings, output and retry history. Later teardown output appears in a fresh `BDTestUtil.GetScenarios(run)` query; previously returned results stay unchanged.

When a step throws:

1. That step is marked with a status derived from the exception (usually `Failed`, see [exception classification](../configuration/exceptions.md)).
2. Steps after it are not executed and stay `NotRun`.
3. The scenario takes the step's status, and BDTest rethrows the exception so your test runner reports the failure.

### A chain runs once

A runnable builder supports one execution. Calling `BDTestAsync()` or awaiting the same builder again, including while it runs or after it fails, throws `BDTest.Exceptions.AlreadyExecutedException` before any steps run again.

Each continuation (for example `Then(...).And(...)`) creates an independent builder with its own execution limit, step state and report entry. Create a new chain to run another scenario. [Retries](retries.md) happen inside the original execution and keep one report entry.

## Overriding scenario text

`[ScenarioText]` on the test method is the usual way to name a scenario (see [Stories and scenarios](stories-and-scenarios.md)). You can also set it on the chain:

```csharp title="Illustrative fragment"
await When(() => Action())
    .Then(() => Check())
    .WithScenarioText("Checking works")
    .And(() => AnotherCheck())
    .BDTestAsync();
```

`WithArgs` and `WithStepText` return the concrete builder on every step kind. `WithScenarioText` does the same on `Then`, `AndThen` and `ButThen`, so you can continue chaining after naming a scenario. Continuations inherit the text at construction; changing one branch later does not rename its siblings. The step kinds are sealed and do not inherit from each other. See the [migration guide](../migration/fluent-builders.md) for explicitly typed variables.

## Tests that are not run

BDTest records every chain it builds. If a chain is built but never executed, it appears in reports as a **not run** test (on the report server's **Not run** tab). This catches tests that forgot to `await` the chain.

Building a chain records its definition: step delegates, lazy arguments, text, tags, metadata and timeouts. Execution copies that definition into fresh scenario and step state. Branches and continuations keep independent configuration, including when a continuation is created after the original chain finishes. A single builder still runs once; retries use the same execution and re-evaluate arguments for each attempt.

Pending reports contain data-only [definition summaries](../advanced/utilities.md#pending-definitions). Reporting never executes pending step, argument or text callbacks. Do not serialize a fluent builder as a report model; use [`BDTestJsonHelper`](../reports/json-data.md).

## Generic step chains

Use `Step(...)` when a workflow does not need Given/When/Then phases. A single
step or any number of chained steps can be awaited directly or executed with
`BDTestAsync()`. All steps display the `Step` keyword, preserve execution order,
and use the usual failure, retry, hook, timeout and reporting behavior.

```csharp sample="NUnit/GenericWorkflowTests.cs" title="Compiled example"
using BDTest;
using NUnit.Framework;

public class GenericWorkflowTests : NUnitBDTestBase<object>
{
    [Test]
    public async Task Records_a_workflow()
    {
        var values = new List<string>();
        await Step(() => values.Add("created"))
            .WithStepText(() => "create a record")
            .Step(async () => { await Task.Yield(); values.Add("saved"); })
            .WithStepText(() => "save the record")
            .Step(() => Assert.That(values, Is.EqualTo(new[] { "created", "saved" })))
            .WithStepText(() => "verify the workflow")
            .BDTestAsync();
    }
}
```

Generic steps accept `Action`, `Func<Task>`, `Func<ValueTask>` and
`Func<ValueTask<T>>`, including cancellation-token variants of asynchronous
steps. `[StepText]`, `WithStepText`, `WithArgs` and `WithScenarioText` work as
usual. The source generator also discovers generic step methods. Report JSON
uses `stepType: "Step"`; existing step-kind values remain unchanged. Generic
chains and typed Given/When/Then chains are separate fluent entry points.
