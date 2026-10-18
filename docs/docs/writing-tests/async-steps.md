---
title: Asynchronous steps
description: Use async steps, await scenarios, and understand how BDTest treats synchronization contexts.
---

# Asynchronous steps

Any `Given`, `When`, `Then`, `And` or `But` accepts a synchronous call or a call that returns `Task`. Mix them freely:

```csharp
[Test]
public async Task CreateAndFetchAccount()
{
    await Given(() => Accounts.ANewCustomer())          // void
        .When(() => Accounts.CreateAccountAsync())      // Task
        .Then(() => Accounts.FetchAccountAsync())       // Task<T>
        .And(() => Assertions.TheAccountIsActive())     // void
        .BDTestAsync();
}
```

Test methods return `Task` and await the scenario, either with `BDTestAsync()` or by awaiting the chain directly:

```csharp
[Test]
public async Task CreateAccount() => await When(() => Accounts.CreateAccountAsync())
    .Then(() => Assertions.TheAccountIsActive());
```

:::warning Never block
Do not call `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on a scenario. Blocking a single-threaded synchronization context can deadlock. The synchronous `.BDTest()` method has been removed for this reason; see [Migrating to async-only execution](../migration/async-only.md).
:::

## `ValueTask` and other awaitables

Asynchronous step expressions must return `Task` or `Task<T>`. Convert a `ValueTask` at the call site so BDTest waits for it and reports its exceptions:

```csharp
await When(() => Repository.SaveAsync().AsTask())
    .Then(() => Assertions.TheItemWasSaved())
    .BDTestAsync();
```

BDTest rejects a step that directly returns a `ValueTask`, a configured awaitable or another custom awaitable before the step runs. Binding it to `Action` would silently discard its asynchronous work. For other awaitables, write an `async Task` helper that awaits the operation and use the helper as the step.

Avoid hiding asynchronous work in a synchronous or `async void` helper. BDTest cannot wait for it.

## Execution order and threads

`BDTestAsync()` and direct `await` run steps in order. BDTest does not schedule a thread-pool task for each scenario or step. Execution begins inline, and each asynchronous step must finish before the next one starts. When every step completes synchronously, the whole scenario can complete on the calling thread.

## Synchronization contexts

BDTest preserves the caller's synchronization context across steps, [hooks](step-hooks.md), [retries](retries.md) and [outline rows](scenario-outlines.md).

- An asynchronous step resumes on that context unless its own code opts out, for example with `ConfigureAwait(false)`.
- Later steps still start on the caller's context.
- Steps with a [timeout](timeouts.md) run their bodies on the thread pool, so the timeout can bound synchronous work.

When running under a UI or STA context, await the async entry points and keep the context processing messages.

## Sharing data between steps

Use the test's [`Context`](test-context.md) object to pass data from one step to the next. Changes to an `AsyncLocal<T>` value inside a step do not flow to later steps or back to the caller.
