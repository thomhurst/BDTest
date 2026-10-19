---
title: Explicit runs
description: Isolate scenario storage and reporting with BDTestRun in custom adapters and long-lived hosts.
---

# Explicit runs

Built-in adapters use [isolated assembly runs](../configuration/assembly-runs.md). Custom adapters and long-lived hosts explicitly own a `BDTestRun` to keep their scenarios, options and reports separate. There is no implicit process-wide default run.

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
- Every report export requires a run. Pass the adapter assembly run explicitly.
- Upload overloads accept a final `CancellationToken`, for example `BDTestReportServer.SendDataAndGetReportUriAsync(serverAddress, descriptor, httpClient, run, cancellationToken)`. Cancellation stops warm-up, requests and retry waits while preserving run isolation and caller-owned client lifetime; see [upload cancellation](../report-server/sending-results.md#cancellation). Await completion or cancellation before disposing the run.

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
options.ScenarioHooks.Add(new MyScenarioHook());

using var run = BDTestRun.Start(options);
```

See the [options reference](../configuration/settings.md) for all registrations.

- `Start(options)` copies the registrations into the run. Later changes to the options affect only runs started afterwards.
- `Start()` uses empty registrations and default debug settings.
- Register any framework outcomes your custom adapter needs on the options. Report metadata comes only from the [descriptor](../reports/overview.md#run-descriptors).
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

### Scenario hooks for a run

Register `BDTest.Interfaces.IScenarioHook` instances on `ScenarioHooks`. Hooks receive the scenario, its context, a zero-based attempt number and an attempt cancellation token. Run hooks surround the instance's scenario hooks on every attempt, including skips, retries and outline rows. See [Scenario hooks](../writing-tests/scenario-hooks.md) for ordering, exception rules and timing.

## Execution clock (.NET 8+)

The modern core build accepts a thread-safe `TimeProvider` on run options:

```csharp
var options = new BDTestOptions { TimeProvider = TimeProvider.System };
using var run = BDTestRun.Start(options);
```

The provider supplies UTC execution timestamps, monotonic elapsed durations, and step/scenario timeout timers. Scenario outlines use the same clock for parent timing and factory-error rows. Empty reports use the run clock for their finish timestamp. Pass a fake provider to test timing without real delays. Advance it after the timed step has started and its timer is registered. Application delays still use their own clocks unless you explicitly pass the same provider to them.

BDTest copies the provider reference when the run starts. Keep it alive until the run and any timed-out work finish. Changing `options.TimeProvider` later does not affect an existing run. Null is rejected. The .NET Standard asset retains system UTC timestamps and monotonic `Stopwatch` timing; it does not expose the provider option.
