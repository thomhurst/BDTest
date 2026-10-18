---
title: Custom adapters
description: Connect BDTest to another test runner with BeginTest and EndTest, exception registrations and skip handling.
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
    static MyRunnerBDTestBase()
    {
        // Register the runner's special exceptions once.
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(MyRunnerSkipException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(MyRunnerSkipException));
        }
    }

    // Called by the runner before each test, before any async setup starts.
    [MyRunnerBeforeTest]
    public void BDTestSetUp() => BeginTest(MyRunner.CurrentTest.Id);

    // Called by the runner after each test.
    [MyRunnerAfterTest]
    public void BDTestTearDown() => EndTest();

    // Map a whole-scenario skip onto the runner.
    protected override void OnScenarioSkipped(string reason) => throw new MyRunnerSkipException(reason);
}
```

| Member | Purpose |
| --- | --- |
| `BeginTest(string frameworkTestId)` | Starts one test execution. Records the execution ID and the owning `TestRun`, installs console capture and clears stale capture state. |
| `EndTest()` | Ends the execution. Removes its context and pending output from the owning run, then clears matching flow state. Calling it again is safe and never completes another active execution. |
| `OnScenarioSkipped(string reason)` | Called when a whole scenario is skipped. Throw the runner's skip exception or call its skip API. |
| `TestRun` | The [`BDTestRun`](../advanced/explicit-runs.md) that stores this test's scenarios. Override it with a stable run to isolate scenarios, output and completion from other runs. Defaults to the shared run. |

Adapters no longer need to set `TestOutputData.FrameworkExecutionId` or call `RemoveContext()` themselves.

If your adapter needs BDTest internals, it must be added to `InternalsVisibleTo` in `BDTest/BDTest.csproj` and be strong-name signed with the repository key. Adapters that only use the public API need neither.

## Execution identity

Every test execution needs its own ID so BDTest can keep its context and output separate. Pass one stable ID to `BeginTest` for each execution, including every data row, repeat and parallel test.

The built-in adapters use:

| Adapter | ID |
| --- | --- |
| TUnit | `TestContext.Current.Id` |
| NUnit | `Test.ID` plus the repeat count |
| xUnit v3 | `TestContext.Current.Test.UniqueID` |
| MSTest | class name and data-row display name, plus an execution suffix |

Call `BeginTest` **synchronously** in the flow that runs setup and the test body, before any async work starts, and call `EndTest()` in teardown. Keep both calls in the same async entry method:

```csharp
public async Task RunTestAsync(string frameworkTestId)
{
    BeginTest(frameworkTestId);
    try
    {
        await SetupAsync();
        await When(() => ActAsync()).Then(() => Verify()).BDTestAsync();
    }
    finally
    {
        EndTest();
    }
}
```

`BeginTest` stores its state in `AsyncLocal` values. Calling it inside a separate async setup method or `Task.Run` does not flow those values back to the caller. Runners that isolate hook execution contexts need to export them; the TUnit adapter calls `TestContext.Current.AddAsyncLocalValues()` for this reason.

### Migrating older adapters

`MarkTestAsStarted()` and `MarkTestAsComplete()` are obsolete compatibility shims. Replace them with `BeginTest(id)` and `EndTest()`. Existing `BDTestExecutionId` overrides still work, but new adapters should pass the ID to `BeginTest` instead.

## Testing your adapter

The repository's framework test projects (`BDTest.Tests`, `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, `BDTest.TUnit.Tests`) focus on unique IDs and context isolation under parallel execution. Use them as a template. `scripts/Test-SkipOutcomes.ps1` checks runner skip outcomes against BDTest's scenario and step statuses.
