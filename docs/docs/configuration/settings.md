---
title: Settings
description: Reference for BDTestSettings, the global configuration for BDTest.
---

# Settings

Built-in adapters use [assembly configuration](assembly-runs.md) with frozen `BDTestOptions`. Configure rules, hooks and converters there. Explicit runs also use [run options](../advanced/explicit-runs.md#run-options).

The reference below describes **legacy standalone/default-run compatibility only**. `BDTest.Settings.BDTestSettings` execution settings do not configure NUnit, xUnit, MSTest or TUnit assembly runs. Legacy report metadata and custom properties still apply at export time. These static APIs will be removed by [#954](https://github.com/thomhurst/BDTest/issues/954).

## Reference

| Member | Type | Description |
| --- | --- | --- |
| `Debug.ShouldSkipWhenStep` | `bool` | Skip every `When` step. See [Debugging tests](#debugging-tests). |
| `CustomExceptionSettings` | `CustomExceptionSettings` | Success, skip and inconclusive exception types. See [Exception classification](exceptions.md). |
| `RegisterCustomStringConverter<T>(converter)` | method | Add an `IStepTextStringConverter<T>`. See [Step text](../writing-tests/step-text.md#custom-converters). |
| `RemoveCustomStringConverter<T>(converter)` | method | Remove the first equal converter. Returns whether one was found. |
| `CustomStringConverters` | `List<object>` | Legacy converter list. Prefer the methods above. |
| `GlobalSkipStepRules` | `SkipStepRules` | `Add<TAttribute>(Func<TAttribute, bool>)` for `SkipStepAttribute` subclasses. See [Skipping](../writing-tests/skipping.md). |
| `GlobalSkipTestRules` | `SkipTestRules` | `Add(Func<Scenario, bool>)` to skip whole scenarios. |
| `GlobalRetryTestRules` | `RetryTestRules` | `Add(Func<Exception, bool>, int retryLimit)`. See [Retries](../writing-tests/retries.md). |
| `BeforeStep` | `event Func<Step, Task>` | Global hook before each step. See [Step hooks](../writing-tests/step-hooks.md). |
| `AfterStep` | `event Func<Step, Task>` | Global hook after each step. |
| `BeforeScenario` | `event Func<ScenarioHookContext, Task>` | Global hook before each scenario attempt. See [Scenario hooks](../writing-tests/scenario-hooks.md). |
| `AfterScenario` | `event Func<ScenarioHookContext, Task>` | Global hook after each scenario attempt, including skips and failures. |

### Obsolete members

| Member | Replacement |
| --- | --- |
| `Environment`, `Tag`, `BranchName` | Set these on a `BDTestRunDescriptor` passed to the report helpers. The settings are still used as fallbacks when a descriptor value is missing. |
| `CustomProperties` | Copied into JSON, HTML and uploaded reports at export time, including explicit runs; still obsolete. |

Settings in older wiki pages, such as `PersistentResultsDirectory`, `ReportFolderName` and the report file name settings, no longer exist. Reports are now produced by the [HTML and JSON helpers](../reports/overview.md) or the [report server](../report-server/overview.md).

## Debugging tests

```csharp
// Inside IBDTestConfiguration.Configure(BDTestOptions options):
options.Debug.ShouldSkipWhenStep = true;
```

When `true`, BDTest does not execute `When` steps (including `And` and `But` steps in the `When` phase). Use it to find tests that do not test what you think they test:

- With the action skipped, you would expect every test to fail.
- A test that still passes is probably not asserting on the result of its action.

Scenarios with skipped `When` steps and passing `Then` steps are reported as `Passed`, so look for passes in the report. Use this setting only while checking your tests; do not commit it.

## Thread safety

Settings are global for the process and shared by tests that run in parallel.

- Converter registration and removal, rule collections, exception collections and hook subscriptions are safe to use concurrently.
- Hooks and converters may run concurrently for different tests. Keep their state thread-safe.
- Prefer configuring everything once, before any test starts.
