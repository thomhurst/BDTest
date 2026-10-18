---
title: Explicit runs
description: Isolate scenario storage and reporting with BDTestRun in custom adapters and long-lived hosts.
---

# Explicit runs

By default, every scenario in a process goes into one shared store, and the report helpers read from it. Custom adapters and long-lived hosts can own a `BDTestRun` instead, to keep a separate set of scenarios and produce a separate report.

```csharp
using var run = BDTestRun.Start();

var tests = new MyTests(run);
await tests.When(() => tests.Act()).Then(() => tests.Check()).BDTestAsync();

var scenarios = BDTestUtil.GetScenarios(run);
var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);

public class MyTests(BDTestRun run) : BDTestBase
{
    public override BDTestRun TestRun => run;

    public void Act() { }
    public void Check() { }
}
```

## Rules

- Override `TestRun` and keep it stable for the test instance. Builders and scenarios capture their run, including builders executed later and scenario outline rows.
- Each run has a new `ReportId` and its own stores for scenarios, unfinished definitions, listeners and output. An explicit run also has its own executor.
- The JSON and HTML helpers and the report upload accept a run, for example `BDTestReportServer.SendDataAndGetReportUriAsync(run, serverAddress, descriptor)`.
- Existing overloads and the framework adapters use the shared default run.

## Lifetime

Dispose a run only after its tests, output writers and attachment exporters finish. Disposal clears its stores, deletes its temporary attachment files, and prevents new scenarios from being constructed or executed. It does not cancel active tests.

`BDTestUtil.ClearScenarios(run)` clears storage between executions and keeps the same report ID. Start a new run to get a new ID.

## Run options

Pass `BDTestOptions` to `BDTestRun.Start` to configure an explicit run:

```csharp
var options = new BDTestOptions();
options.SuccessExceptionTypes.Add(typeof(MySuccessException));
options.SkipExceptionTypes.Add(typeof(MySkipException));
options.RetryTestRules.Add(exception => exception is IOException, retryLimit: 2);
options.SkipTestRules.Add(scenario => scenario.Tags.Contains("manual"));
options.SkipStepRules.Add<MySkipAttribute>(attribute => attribute.ShouldSkip);
options.Debug.ShouldSkipWhenStep = false;
options.RegisterCustomStringConverter(new MyValueConverter());
options.StepHooks.Add(new MyStepHook());

using var run = BDTestRun.Start(options);
```

| Option | Equivalent global setting |
| --- | --- |
| `SuccessExceptionTypes`, `SkipExceptionTypes`, `InconclusiveExceptionTypes` | [`CustomExceptionSettings`](../configuration/exceptions.md) |
| `RetryTestRules` | `GlobalRetryTestRules` |
| `SkipTestRules` | `GlobalSkipTestRules` |
| `SkipStepRules` | `GlobalSkipStepRules` |
| `Debug` | `Debug` |
| `RegisterCustomStringConverter<T>(...)` | `RegisterCustomStringConverter<T>(...)` |
| `StepHooks` (`IStepHook`) | `BeforeStep` / `AfterStep` events |
| `Attachments` ([byte limits](../writing-tests/attachments.md#limits)) | Fixed defaults on the shared default run |

- `Start(options)` copies the registrations into the run. Later changes to the options affect only runs started afterwards.
- `Start()` uses empty registrations and default debug settings.
- **Explicit runs do not inherit `BDTestSettings`.** Register any framework exceptions your adapter needs on the options.
- Configure options before starting a run, without concurrent changes. Callback, converter and hook instances are shared, not cloned, so they must be thread-safe.

### Step hooks for a run

Implement `BDTest.Interfaces.IStepHook`:

```csharp
public sealed class TimingHook : IStepHook
{
    public Task BeforeStepAsync(Step step) => Task.CompletedTask;

    public Task AfterStepAsync(Step step)
    {
        Console.WriteLine($"{step.StepText} took {step.TimeTaken}");
        return Task.CompletedTask;
    }
}
```

Run hooks run sequentially in registration order. Before hooks run before the test instance's `OnBeforeStep`; after hooks run after `OnAfterStep`. They also run for reached skipped steps and on retries. Every after hook runs even if another hook fails, and an original step failure takes precedence over after-hook failures.

## The default run

`BDTestSettings` configures the shared default run used by the framework adapters and the overloads without a run parameter.
