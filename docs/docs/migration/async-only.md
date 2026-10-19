---
title: Migrating to async-only execution
description: Replace the removed synchronous BDTest() entry points with awaited BDTestAsync() calls.
---

# Migrating to async-only execution

The next major release removes `RunnableStepBuilder.BDTest()` and `ScenarioOutlineBuilder<TExample>.BDTest()`. Use `BDTestAsync()` or await the builder directly. This change is tracked by [#801](https://github.com/thomhurst/BDTest/issues/801). The asynchronous patterns below also work with earlier releases, so you can migrate before upgrading.

## Test methods

Change `void` to `async Task`, add `await`, and remove `.BDTest()`:

```csharp title="Illustrative fragment"
// Before
[Test]
public void Login() => Given(() => User()).When(() => LogIn()).Then(() => LoggedIn()).BDTest();

// After: direct await
[Test]
public async Task Login() => await Given(() => User()).When(() => LogIn()).Then(() => LoggedIn());

// After: explicit execution
[Test]
public async Task LoginExplicitly() => await Given(() => User())
    .When(() => LogIn()).Then(() => LoggedIn()).BDTestAsync();
```

NUnit, xUnit, MSTest and TUnit all support `Task` test methods. Step methods may stay synchronous. Await any test helpers that now return tasks.

## Outlines and context helpers

```csharp title="Illustrative fragment"
[Test]
public async Task Logins() => await ScenarioOutline("Login",
    new[] { new { Name = "Alice" }, new { Name = "Bob" } },
    row => Given(() => User(row.Name)).When(() => LogIn()).Then(() => LoggedIn()));

[Test]
public async Task WithSharedState()
{
    var context = new MyContext();
    await Given(() => Prepare(context))
        .When(() => Act(context)).Then(() => Verify(context)).BDTestAsync();
}
```

Builders still execute only once. Running the same chain or outline again throws `AlreadyExecutedException`, including after a failure or while the first execution is running. Create a new chain or outline to run again. Configured retries remain part of the original execution.

Use `BDTestAsync(cancellationToken)` when execution must observe a caller token. The parameterless overload and direct `await` still work. A caller token alone does not move work onto the thread pool, so cancellation cannot interrupt a synchronous or non-yielding prefix without a timeout. See [Timeouts and cancellation](../writing-tests/timeouts.md).

## Do not block

Do not replace `.BDTest()` with `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`. Blocking can deadlock UI, STA and other single-threaded contexts. Keep the context processing messages while awaiting.

## Behaviour changes

- Scenario execution starts inline, without a scenario-level `Task.Run`.
- Without an active timeout, steps, hooks, retries and outline rows preserve the caller's synchronization context. An async step resumes there unless its own code opts out, for example with `ConfigureAwait(false)`. Later steps still start on the caller's context.
- Timeout-enabled operations, including hooks and attempt preparation, run on the thread pool so the timeout can bound synchronous work. Already-cancelled cleanup also uses the thread pool with a bounded grace period.
- If a test relied on the old thread-pool scheduling, schedule that work explicitly.
- Use the fixture's `Context` to share state between steps. `AsyncLocal` changes inside a step do not flow back to the caller.

No analyzer or automatic code fix is included. See [Asynchronous steps](../writing-tests/async-steps.md) for the full execution model.
