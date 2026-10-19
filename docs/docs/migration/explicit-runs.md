---
title: Explicit run ownership
description: Migrate global settings, hooks, builders and report helpers to owned runs.
---

# Explicit run ownership

The next major version removes `BDTestSettings`, `CustomExceptionSettings`, static
step/scenario events and the implicit process-wide default run. Each run owns a
frozen configuration, executor, report ID, scenario store and output buffers.

## Configure before starting

For NUnit, xUnit, MSTest and TUnit, use the existing
[assembly configuration](../configuration/assembly-runs.md) and assembly owner.
Standalone callers and custom adapters create their own run:

```csharp
var options = new BDTestOptions();
options.RetryTestRules.Add(error => error is IOException, retryLimit: 2);
options.SkipExceptionTypes.Add(typeof(MySkipException));
options.RegisterCustomStringConverter<MyValue>(new MyValueConverter());
options.StepHooks.Add(new MyStepHook());
options.ScenarioHooks.Add(new MyScenarioHook());
using var run = BDTestRun.Start(options);
var builder = new BDTestBuilder(run);
await builder.When(() => Act()).Then(() => Check()).BDTestAsync();
var json = BDTestJsonHelper.GetTestJsonData(null, run);
```

| Removed API | Replacement |
| --- | --- |
| `BDTestSettings.CustomExceptionSettings` | Exception lists and `RegisterFrameworkOutcomes` on `BDTestOptions` |
| `GlobalRetryTestRules`, `GlobalSkipTestRules`, `GlobalSkipStepRules` | `RetryTestRules`, `SkipTestRules`, `SkipStepRules` on options |
| Static `BeforeStep` / `AfterStep` events | `IStepHook` instances in `options.StepHooks` |
| Static `BeforeScenario` / `AfterScenario` events | `IScenarioHook` instances in `options.ScenarioHooks` |
| Global converter list and register/remove methods | Typed `options.RegisterCustomStringConverter<T>(converter)` before starting |
| `new BDTestBuilder()` | `new BDTestBuilder(run)` |
| `BDTestScenario.For<TContext>(instance)` | `BDTestScenario.For<TContext>(instance, run)` |
| No-run store utilities or report overloads | Pass the owning run explicitly |
| `BDTestUtil.CurrentReportId` | `run.ReportId` |
| Global report labels and custom properties | `BDTestRunDescriptor` passed to export |

Custom classes derived from `BDTestBase` or `AbstractContextBDTestBase<TContext>`
must implement the abstract `TestRun` property with a stable owned run. Keep
`BeginTest` and `EndTestAsync`/`EndTest` lifecycle calls; no additional lifecycle
system is needed. Core composition requires a run, while framework composition
factories can resolve their configured assembly run.

Registrations freeze at `Start(options)`. Later option mutations do not affect
existing runs, including retries. Typed converters use the registered generic
contract, even when an object implements several converter interfaces. Callbacks
remain shared references and must protect mutable state. See [hooks](../writing-tests/step-hooks.md)
and [options](../configuration/settings.md).

## Export and dispose

```csharp
var descriptor = new BDTestRunDescriptor { Tag = "Nightly", Environment = "Staging" };
descriptor.CustomProperties["Build"] = buildNumber;
var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);
var scenarios = BDTestUtil.GetScenarios(run);
await BDTestReportServer.SendDataAndGetReportUriAsync(
    serverAddress, descriptor, httpClient, run, cancellationToken);
```

Every export uses only the selected run and supplied descriptor. Missing metadata
stays null; no global values are inherited. Custom properties are copied at export,
so avoid changing a descriptor concurrently. Historical JSON custom properties
remain readable. Report-only timers have unset timestamps until supplied by the
report or calculated from scenarios; an empty run export uses that run's start and
export times.

Export before disposing the run or its assembly owner. Disposal synchronously
clears stores and temporary attachment files and prevents new executions; it does
not cancel work in progress. A new run starts empty with a fresh report ID.
`BDTestUtil.ClearScenarios(run)` clears the current store without changing its ID.
