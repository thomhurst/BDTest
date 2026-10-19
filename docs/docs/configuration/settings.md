---
title: Settings
description: Configure frozen BDTestOptions for an assembly or an explicitly owned run.
---

# Settings

Built-in adapters use [assembly configuration](assembly-runs.md). Standalone callers configure `BDTestOptions` before `BDTestRun.Start(options)`. Every run freezes its registrations at startup. There is no process-wide settings object or default run.

## Reference

| Member on `BDTestOptions` | Description |
| --- | --- |
| `Debug.ShouldSkipWhenStep` | Skip every `When` step. See [Debugging tests](#debugging-tests). |
| `SuccessExceptionTypes`, `SkipExceptionTypes`, `InconclusiveExceptionTypes` | [Exception classification](exceptions.md). |
| `RegisterFrameworkOutcomes(map)` | Register framework exception types and outcome callbacks. |
| `RegisterCustomStringConverter<T>(converter)` | Add a typed converter. See [Step text](../writing-tests/step-text.md#custom-converters). |
| `SkipStepRules` | Attribute-based predicates; see [Skipping](../writing-tests/skipping.md). |
| `SkipTestRules` | Predicates evaluated once before scenario execution; a predicate error fails without retrying. |
| `RetryTestRules` | Default exception predicates and retry limits, replaced by explicit retry attributes; see [Retries](../writing-tests/retries.md). |
| `StepHooks` | Ordered `IStepHook` instances; see [Step hooks](../writing-tests/step-hooks.md). |
| `ScenarioHooks` | Ordered `IScenarioHook` instances; see [Scenario hooks](../writing-tests/scenario-hooks.md). |
| `ContextFactory`, `Services` | [Context creation](../writing-tests/test-context.md#factories). |
| `Attachments` | [Capture limits](../writing-tests/attachments.md#limits). |
| `TimeProvider` (.NET 8+) | [Execution clock](../advanced/explicit-runs.md#execution-clock-net-8). |

Put report labels and custom properties on `BDTestRunDescriptor`, passed to [report helpers](../reports/overview.md#run-descriptors). See [migration from global settings](../migration/explicit-runs.md).

## Debugging tests

```csharp title="Illustrative fragment"
// Inside IBDTestConfiguration.Configure(BDTestOptions options):
options.Debug.ShouldSkipWhenStep = true;
```

When `true`, BDTest does not execute `When` steps (including `And` and `But` steps in the `When` phase). Use it to find tests that do not test what you think they test:

- With the action skipped, you would expect every test to fail.
- A test that still passes is probably not asserting on the result of its action.

Scenarios with skipped `When` steps and passing `Then` steps are reported as `Passed`, so look for passes in the report. Use this setting only while checking your tests; do not commit it.

## Thread safety

Configure options before starting a run, without concurrent mutations. Later registration changes affect only newly started runs. Callback, converter and hook instances are copied by reference, so protect their mutable state when tests run concurrently. Keep their resources alive until the run has finished.
