---
title: Custom adapter lifecycle
---

Custom adapters deriving from `BDTestBase` or
`AbstractContextBDTestBase<TContext>` call `BeginTest(frameworkTestId)` before
async setup and `EndTest()` during teardown. Supply one stable ID for each test
execution, including every data row, repeat, and parallel test.

```csharp
public async Task RunTestAsync(string frameworkTestId)
{
    BeginTest(frameworkTestId);
    try
    {
        await SetupAsync();
        await RunScenarioAsync();
    }
    finally
    {
        EndTest();
    }
}
```

Call `BeginTest` synchronously in the flow that runs setup and the test body.
Changes to `AsyncLocal` values inside a separate async setup method or `Task.Run`
do not propagate back to the caller. TUnit hooks export their values through
`TestContext.Current.AddAsyncLocalValues()`.

`BeginTest` records the execution ID and owning `TestRun`, installs console
capture, and clears stale capture state. Override `TestRun` with a stable run to
keep scenarios, output, and completion isolated from other runs. `EndTest`
removes that execution's context and pending output from its owning run, then
clears matching flow state. Repeated completion is safe and does not complete
another active execution.

The built-in adapters use NUnit's test ID plus repeat count, xUnit v3's test ID,
TUnit's context ID, and MSTest's class/data-row display name plus an execution
suffix. The xUnit adapter buffers constructor output until initialization; see
its [initialization and teardown guide](../frameworks/xunit.md).

Migrate custom adapters from `MarkTestAsStarted()` and `MarkTestAsComplete()` to
`BeginTest(id)` and `EndTest()`. The old methods remain obsolete compatibility
shims. Existing `BDTestExecutionId` overrides remain supported, but new adapters
should pass the ID to `BeginTest` instead. Adapters no longer need to assign
`TestOutputData.FrameworkExecutionId` or call `RemoveContext()` themselves.
