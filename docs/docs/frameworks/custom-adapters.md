---
title: Custom adapters
description: Connect BDTest to another test runner by supplying execution identity, lifecycle calls and exception registrations.
---

# Custom adapters

An adapter connects BDTest to a test runner. The built-in adapters are small; each is a single base class. Write your own when you use a runner without an adapter, or when you need different lifecycle behaviour.

## Start from `AbstractContextBDTestBase<TContext>`

```csharp
using BDTest.Settings;
using BDTest.Test;

public abstract class MyRunnerBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class, new()
{
    // 1. A stable, unique ID for the running test.
    protected override string BDTestExecutionId => MyRunner.CurrentTest.Id;

    static MyRunnerBDTestBase()
    {
        // 2. Register the runner's special exceptions once.
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(MyRunnerSkipException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(MyRunnerSkipException));
        }
    }

    // 3. Map a whole-scenario skip onto the runner.
    protected override void OnScenarioSkipped(string reason) => throw new MyRunnerSkipException(reason);

    // 4. Called by the runner after each test.
    [MyRunnerAfterTest]
    public void BDTestTearDown()
    {
        RemoveContext();
        MarkTestAsComplete();
    }
}
```

| Member | Purpose |
| --- | --- |
| `BDTestExecutionId` | Keys the context, output and scenario for one test. Must be unique per test execution and stable for its duration. |
| `MarkTestAsStarted()` | Starts a new execution when you rely on the default execution ID instead of overriding it. |
| `MarkTestAsComplete()` | Completes the test. Call it in teardown. |
| `RemoveContext()` | Removes the test's context. Call it in teardown. |
| `OnScenarioSkipped(string reason)` | Called when a whole scenario is skipped. Throw the runner's skip exception or call its skip API. |
| `TestRun` | The [`BDTestRun`](../advanced/explicit-runs.md) that stores this test's scenarios. Defaults to the shared run. |

If your adapter needs BDTest internals, it must be added to `InternalsVisibleTo` in `BDTest/BDTest.csproj` and be strong-name signed with the repository key. Adapters that only use the public API need neither.

## Execution identity

Every test needs an execution ID so that BDTest can keep its context and output separate from other tests.

**Preferred:** override `BDTestExecutionId` with the runner's stable per-test ID, as the NUnit, xUnit and TUnit adapters do.

**Otherwise:** call the protected `MarkTestAsStarted()` **synchronously** in the flow that runs setup and the test, before any async work starts, and call `MarkTestAsComplete()` in teardown. Keep both calls inside the same async entry method so its identity does not leak into the caller:

```csharp
public async Task RunTestAsync()
{
    MarkTestAsStarted();
    try
    {
        await SetupAsync();
        await When(() => ActAsync()).Then(() => Verify()).BDTestAsync();
    }
    finally
    {
        MarkTestAsComplete();
    }
}
```

Calling the start method inside a separate async setup method, or inside `Task.Run`, does not flow its `AsyncLocal` value back to the caller. For compatibility, the default implementation still creates an ID on first access. Custom adapters with async setup should use the explicit lifecycle or a stable framework ID instead.

The MSTest adapter shows a third option: assign a GUID per fixture instance when the runner creates one instance per test.

## Testing your adapter

The repository's framework test projects (`BDTest.Tests`, `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, `BDTest.TUnit.Tests`) focus on unique IDs and context isolation under parallel execution. Use them as a template. `scripts/Test-SkipOutcomes.ps1` checks runner skip outcomes against BDTest's scenario and step statuses.
