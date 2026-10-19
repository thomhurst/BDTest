---
title: Custom adapters
description: Connect BDTest to another test runner with BeginTest and EndTest, exception registrations and skip handling.
---

# Custom adapters

An adapter connects BDTest to a test runner. The built-in adapters offer base classes; NUnit and TUnit also offer framework-native composition. Write your own when you use a runner without an adapter, or when you need different lifecycle behaviour.

To integrate without requiring test-class inheritance, create a [composition host](composition.md) with the runner's test instance and an explicit run. Its public `BeginTest` and `EndTest` methods use the same core lifecycle described below. Register the runner's outcome map on the run options before starting the run.

## Start from `AbstractContextBDTestBase<TContext>`

```csharp title="Illustrative fragment"
using BDTest;
using BDTest.Settings;

public abstract class MyRunnerBDTestBase<TContext> : AbstractContextBDTestBase<TContext>
    where TContext : class
{
    private static readonly FrameworkOutcomeMap RunnerOutcomes =
        new FrameworkOutcomeMap(reason => throw new MyRunnerSkipException(reason))
            .Skip<MyRunnerSkipException>();

    protected MyRunnerBDTestBase(BDTestRun run) => TestRun = run;
    public override BDTestRun TestRun { get; }

    // The runner's assembly owner configures and starts the run once:
    // options.RegisterFrameworkOutcomes(RunnerOutcomes);
    // run = BDTestRun.Start(options);
    // Pass run into fixtures; export and dispose after all tests finish.

    // Called by the runner before each test, before any async setup starts.
    [MyRunnerBeforeTest]
    public void BDTestSetUp() => BeginTest(MyRunner.CurrentTest.Id, MyRunner.CurrentTest.MethodInfo);

    // Called by the runner after each test.
    [MyRunnerAfterTest]
    public async Task BDTestTearDown() => await EndTestAsync();

    // Map a whole-scenario skip onto the runner.
    protected override void OnScenarioSkipped(string reason) => RunnerOutcomes.ReportSkip(reason);
}
```

| Member | Purpose |
| --- | --- |
| `BeginTest(string frameworkTestId, MethodInfo testMethod)` | Starts one test execution. Records the execution ID, exact method metadata and the owning `TestRun`, installs console capture and clears stale capture state. |
| `EndTestAsync()` | Awaits owned context disposal, removes pending output from the owning run and clears matching flow state, even when disposal throws. Calling it again is safe. |
| `EndTest()` | Synchronous compatibility bridge for cleanup. Prefer awaiting `EndTestAsync()` for async contexts. |
| `OnScenarioSkipped(string reason)` | Called when a whole scenario is skipped. Throw the runner's skip exception or call its skip API. |
| `TestRun` | The [`BDTestRun`](../advanced/explicit-runs.md) that stores this test's scenarios. Override it with a stable run to isolate scenarios, output and completion from other runs. This abstract property must return an explicitly owned run. |

Adapters no longer need to set `TestOutputData.FrameworkExecutionId` or call `RemoveContext()` themselves.

Official adapters use only the public and protected API. Core internals are
reserved for repository tests. Third-party adapters need neither a friend grant
nor the repository signing key.

### Constructor output and assertion scopes

When the runner constructs a fixture before its owned `TestRun` is available,
call `CaptureStartupOutput(Action beginExecution)` synchronously in the adapter
constructor. This buffers console writes without reading `TestRun`. The callback
must call `BeginTest(id, methodInfo)` and then `BindStartupOutput()` after the
derived constructor finishes. Binding moves buffered text to that run and keeps
constructor-started tasks attached to the same execution.

A runner hook that can run even when the fixture reimplements async setup should
call the protected static `BeginPendingTestExecution()` in the test body flow.
It invokes the pending buffer's callback. Keep that callback idempotent. Call
`CompleteStartupOutput()` before completing teardown, including failed setup and
legacy teardown paths. Completion stops late constructor-task writes and clears
only the current fixture's pending buffer. Then call `EndTestAsync()` (or the
synchronous compatibility hook). Do not resolve an owned run in a base
constructor or use display names as execution IDs.

An adapter with runner assertion state can override the protected
`ExecuteScenarioStepsAsync(Func<Task> executeSteps, Action<Action> registerTerminalFailure)`.
The core calls it for attempt preparation, steps and hooks. Await `executeSteps`
once inside the runner's assertion scope and preserve the original exception so
retry rules see it. Optional diagnostics registered through
`registerTerminalFailure` must throw when invoked; the core invokes them only for
a terminal failed attempt. Preserve warnings and failures outside the scenario.
Do not overwrite the runner's overall result. NUnit uses this hook with NUnit's
public exception constraints, including multiple-assert retry handling.

## Execution identity

Every test execution needs its own ID so BDTest can keep its context and output separate. Pass one stable ID to `BeginTest` for each execution, including every data row, repeat and parallel test.

The built-in adapters use:

| Adapter | ID |
| --- | --- |
| TUnit | `TestContext.Current.Id` |
| NUnit | `Test.ID` plus the repeat count |
| xUnit v3 | `TestContext.Current.Test.UniqueID` |
| MSTest | class name and data-row display name, plus an execution suffix |

Call `BeginTest` **synchronously** in the flow that runs setup and the test body, before any async work starts, and await `EndTestAsync()` in teardown. Keep both calls in the same async entry method:

```csharp title="Illustrative fragment"
public async Task RunTestAsync(string frameworkTestId, System.Reflection.MethodInfo testMethod)
{
    BeginTest(frameworkTestId, testMethod);
    try
    {
        await SetupAsync();
        await When(() => ActAsync()).Then(() => Verify()).BDTestAsync();
    }
    finally
    {
        await EndTestAsync();
    }
}
```

`BeginTest` stores its state in `AsyncLocal` values. Calling it inside a separate async setup method or `Task.Run` does not flow those values back to the caller. Runners that isolate hook execution contexts need to export them; the TUnit adapter calls `TestContext.Current.AddAsyncLocalValues()` for this reason.

### Migrating older adapters

`MarkTestAsStarted()` and `MarkTestAsComplete()` are obsolete compatibility shims. Replace them with `BeginTest(id, testMethod)` and `EndTestAsync()`. Existing `BDTestExecutionId` overrides still work, but new adapters should pass the ID to `BeginTest` instead.

## Testing your adapter

The repository's framework test projects (`BDTest.NUnit.Tests`, `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, `BDTest.TUnit.Tests`) focus on unique IDs and context isolation under parallel execution. Use them as a template. `scripts/Test-SkipOutcomes.ps1` checks runner skip outcomes against BDTest's scenario and step statuses. The shared `TestUtilities/AdapterConformanceTests.cs` suite covers all outcome categories and concurrent explicit runs. `scripts/Test-AdapterConformance.ps1` verifies its actual TRX outcomes in CI.

Declare framework exception classifications and runner notifications with
[`FrameworkOutcomeMap`](../configuration/exceptions.md#framework-outcome-maps).
Register the map on `BDTestOptions` before starting a run. Runs use their frozen
map's skip callback before falling back to `OnScenarioSkipped`.

## Method identity and migration

Pass the runner's actual `System.Reflection.MethodInfo`, including the exact overload, to `BeginTest(id, testMethod)`. Do not look up a test by its display name. Data rows reuse the method metadata while retaining distinct execution IDs. The core caches scenario text, parameter names, combined class/method tags and the retry attribute count by method and concrete fixture type. Scenario arrays are separate copies. Test information attributes are constructed from the cached method identity for each scenario, so mutable fields in custom attribute subclasses are not shared between executions.

BDTest no longer discovers test metadata from the call stack. Move `[ScenarioText]`, `[TestInformation]` and `[BDTestRetry]` attributes from scenario-construction helpers to the test method. Class and assembly retry defaults and class tags continue to apply. Exception stack traces remain in diagnostics and reports.

The public `StackFramesHelper` and `TestInformationAttributeHelper` have been removed for the next major release. Custom adapters should use the lifecycle method above instead. `GetScenarioText` on the base and context now accepts an optional compiler-supplied caller member name.

### Custom harnesses without a runner

`BeginTest(id)` remains available when no `MethodInfo` exists. You may also use `BDTestBase` or `BDTestBuilder` directly. Scenario builders use `CallerMemberName` for humanized scenario text and `CallerFilePath` for the source file. They do not guess method attributes from a name or walk the stack. Class tags and class/assembly retry defaults still apply. Set `.WithScenarioText("...")` after `Then` when the caller name is not the desired scenario title. To use method attributes, call `BeginTest(id, methodInfo)` explicitly before construction.

Implementation text helpers and report converters are internal. Custom adapters
continue to use the protected lifecycle, constructor-output, pending-execution and
assertion-scope methods documented above; they do not need friend access or the
repository signing key. See [helper API migration](../migration/internal-helpers.md).
