---
title: Explicit runs
description: Isolate scenario storage and reporting with BDTestRun in custom adapters and long-lived hosts.
---


Install `BDTest.Reporting` alongside your adapter to export JSON, generate HTML or upload reports.
Use matching package versions; see [reporting package migration](../migration/reporting-package.md).
# Explicit runs

Built-in adapters use [isolated assembly runs](../configuration/assembly-runs.md). Custom adapters and long-lived hosts explicitly own a `BDTestRun` to keep their scenarios, options and reports separate. There is no implicit process-wide default run.

This complete console example references `BDTest` and `BDTest.Reporting`. Use
`BDTest` for execution, `BDTest.Contracts` for immutable result types and
`BDTest.Helpers` for export helpers. It needs no framework adapter or report server.

```csharp sample="Standalone/Program.cs" title="Compiled example"
using BDTest;
using BDTest.Contracts;
using BDTest.Helpers;

using var cancellation = new CancellationTokenSource();
using var run = BDTestRun.Start(new BDTestOptions());
var value = 0;

ScenarioResult result = await new BDTestBuilder(run)
    .Given(() => value = 1)
    .When(() => value += 1)
    .Then(() => Verify(value))
    .BDTestAsync(cancellation.Token);

BDTestRunSnapshot snapshot = run.GetSnapshot();
var descriptor = new BDTestRunDescriptor { Environment = "Local", Tag = "Smoke" };
string json = BDTestJsonHelper.GetTestJsonData(descriptor, run);
BDTestReport report = BDTestJsonHelper.DeserializeReport(json);
string html = BDTestHtmlHelper.GenerateHtmlReport(report);

Console.WriteLine($"{result.Status}: {snapshot.Scenarios.Count} scenario(s)");
Console.WriteLine($"Report {report.Id}: {html.Length} HTML characters");

static void Verify(int value)
{
    if (value != 2) throw new InvalidOperationException("Expected the value to be 2.");
}
```

Create a new `BDTestBuilder(run)` for each scenario; each chain executes once.
The returned result and `GetSnapshot()` values are immutable and remain readable
after run disposal. Attachment bytes remain owned by the live run. Save or upload
them before disposal, which deletes the temporary files.

The example renders saved report data. To export directly from the live run, use
`BDTestHtmlHelper.GetTestHtmlReport(descriptor, run)`. JSON/HTML helpers take the
descriptor first (pass `null` for defaults); the uploader takes the run first:
`await uploader.UploadReportAsync(run, descriptor, cancellation.Token)`. Keep the
run and the caller-owned `HttpClient` alive until upload finishes. See the complete
[upload example](../report-server/sending-results.md#example-with-tunit).

For inheritance, derive from `BDTestBase` and override `TestRun` to return the
owned run. Built-in adapters instead resolve their assembly run: export after all
tests and teardown finish, then dispose through the adapter's assembly owner
(for example `TUnitBDTestAssembly.Dispose(assembly)`). Do not dispose an assembly run
from an individual test.

## Rules

- Override `TestRun` and keep it stable for the test instance. Builders and scenarios capture their run, including builders executed later and scenario outline rows.
- Each run has a new `ReportId` and its own stores for scenarios, unfinished definitions, listeners and output. An explicit run also has its own executor.
- The JSON and HTML helpers and the report upload accept a run, for example `uploader.UploadReportAsync(run, descriptor)`.
- Every report export requires a run. Pass the adapter assembly run explicitly.
- Upload overloads accept a final `CancellationToken`, for example `uploader.UploadReportAsync(run, descriptor, cancellationToken)`. Cancellation stops requests and retry waits while preserving run isolation and caller-owned client lifetime; see [upload cancellation](../report-server/sending-results.md#cancellation). Await completion or cancellation before disposing the run.

## Lifetime

Dispose a run only after its tests, output writers and attachment exporters finish. Disposal clears its stores, deletes its temporary attachment files, and prevents new scenarios from being constructed or executed. It does not cancel active tests. Pass a token to each chain or outline with `BDTestAsync(cancellationToken)` when the host needs to cancel execution.

`BDTestUtil.ClearScenarios(run)` clears storage between executions and keeps the same report ID. Start a new run to get a new ID.

## Run options

Pass `BDTestOptions` to `BDTestRun.Start` to configure an explicit run:

```csharp title="Illustrative fragment"
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

```csharp title="Illustrative fragment"
public sealed class TimingHook : IStepHook
{
    public Task BeforeStepAsync(BDTest.Contracts.StepResult step, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AfterStepAsync(BDTest.Contracts.StepResult step, System.Threading.CancellationToken cancellationToken)
    {
        Console.WriteLine($"{step.StepText} took {step.TimeTaken}");
        return Task.CompletedTask;
    }
}
```

Run hooks run sequentially in registration order. Before hooks run before the test instance's `OnBeforeStep`; after hooks run after `OnAfterStep`. They also run for reached skipped steps and on retries. Every after hook is attempted even if another hook fails, and an original step failure takes precedence over after-hook failures. `IStepHook` methods take a second `CancellationToken` argument for cooperative cancellation. Step and scenario budgets cover hooks, including after hooks. Cancelled cleanup has a bounded grace period; see [Timeouts and cancellation](../writing-tests/timeouts.md).

### Scenario hooks for a run

Register `BDTest.Interfaces.IScenarioHook` instances on `ScenarioHooks`. Hooks receive the scenario, its context, a zero-based attempt number and an attempt cancellation token. Run hooks surround the instance's scenario hooks on every attempt, including skips, retries and outline rows. See [Scenario hooks](../writing-tests/scenario-hooks.md) for ordering, exception rules and timing.

## Execution clock (.NET 8+)

The modern core build accepts a thread-safe `TimeProvider` on run options:

```csharp title="Illustrative fragment"
var options = new BDTestOptions { TimeProvider = TimeProvider.System };
using var run = BDTestRun.Start(options);
```

The provider supplies UTC execution timestamps, monotonic elapsed durations, and step/scenario timeout timers. Scenario outlines use the same clock for parent timing and factory-error rows. Empty reports use the run clock for their finish timestamp. Pass a fake provider to test timing without real delays. Advance it after the timed step has started and its timer is registered. Application delays still use their own clocks unless you explicitly pass the same provider to them. Cancellation cleanup uses a 250 millisecond real-time grace period per operation, independent of this provider.

BDTest copies the provider reference when the run starts. Keep it alive until the run and any timed-out work finish. Changing `options.TimeProvider` later does not affect an existing run. Null is rejected. The .NET Standard asset retains system UTC timestamps and monotonic `Stopwatch` timing; it does not expose the provider option.
