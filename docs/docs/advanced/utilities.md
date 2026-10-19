---
title: Utilities and model
description: Read collected scenarios with BDTestUtil, and the Scenario, Step and BDTestContext types.
---

# Utilities and model

For built-in adapters, pass the [configured assembly run](../configuration/assembly-runs.md) to report/utility helpers. Every export and store utility requires the owning run. Export all reports before disposing through the assembly owner.


## `BDTestUtil`

`BDTest.BDTestUtil` is the public read API over the scenarios collected in the supplied run.

| Member | Returns |
| --- | --- |
| `GetScenarios(BDTestRun)` | Every executed scenario. Outlines appear once, with their rows in `Examples`. |
| `GetNotRunScenarios(BDTestRun)` | `List<ScenarioDefinitionSummary>` snapshots of chains that were built but not executed. |
| `ClearScenarios(BDTestRun)` | Clears collected scenarios. |
| `run.ReportId` (on `BDTestRun`) | The selected run's fresh report ID. |
| `GetTestTimer(IReadOnlyCollection<Scenario> scenarios)` | Start, finish and elapsed time for those scenarios; an empty collection has unset timestamps unless a report timer is supplied. |

Use it to build custom reports or to assert on results in a teardown:

```csharp
var run = BDTest.NUnit.NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var failed = BDTestUtil.GetScenarios(run).Where(scenario => scenario.Status == Status.Failed).ToList();
```

## Pending definitions

`BDTest.Test.ScenarioDefinitionSummary` contains the scenario and story text, `ScenarioId`, `ReportId`, `TestId`, caller details, parameters, tags, test information, scenario `Timeout`, and `Steps`. Each `StepDefinitionSummary` contains `StepType`, unevaluated `StepText` and the step `Timeout`.

These are report snapshots, not fluent builders. Later changes to a chain do not change a previously returned summary. Taking a snapshot never invokes step delegates, `WithArgs`, `WithStepText`, table readers or doc string readers. Step text therefore retains its template placeholders until execution. Identify a pending chain by `ScenarioId`, not by builder object identity.

See [definition migration](../migration/scenario-definitions.md) for the change from `BuildableTest`.

## Scenario and step model

`BDTest.Test.Scenario` is returned by `BDTestAsync()` and stored for reports.

| Member | Description |
| --- | --- |
| `Status` | Final [status](../configuration/statuses.md). |
| `Steps` | `List<Step>` for the final attempt. |
| `GetStoryText()`, `GetScenarioText()` | Resolved story and scenario text. |
| `StartTime`, `EndTime`, `TimeTaken` | Timing of the final attempt. |
| `Exception` | The exception that ended the scenario, if any. |
| `Output` | Console output from all steps. |
| `TestStartupInformation`, `TearDownOutput` | [Setup and teardown output](../writing-tests/output.md#setup-and-teardown-output). |
| `CustomHtmlOutputForReport` | HTML added through `ScenarioHtmlWriter`. |
| `CustomTestInformation` | `[TestInformation]` attributes. |
| `Tags` | [Tags](../writing-tests/tags-and-information.md). |
| `RetryCount`, `Attempts` | [Retry](../writing-tests/retries.md) history. |
| `Timeout`, `TimedOut` | [Timeout](../writing-tests/timeouts.md) configuration and result. |
| `IsOutline`, `Examples`, `ExampleValues`, `OutlineId` | [Scenario outline](../writing-tests/scenario-outlines.md) data. |
| `FrameworkTestId`, `FileName` | The test framework's ID and the source file. |
| `Guid`, `ReportId` | Identifiers. |
| `BdTestBaseClass` | The test class instance, if still available. Completed scenarios keep only a weak reference, so this may be `null` after garbage collection. Deserialized scenarios have no runtime fixture and return `null`. |

Treat `BdTestBaseClass` as a best-effort runtime reference, not retained report data. Check for `null` before using it in teardown or custom reports; store any evidence you need on the scenario while the test runs.

`Scenario.Run` is a nullable runtime reference and is absent from deserialized reports. `Scenario.Exception`, `Step.Exception`, optional identifiers and text also carry nullable annotations. See [nullable report fields](../reports/json-data.md#nullable-report-fields) when consuming results or historical JSON.

`BDTest.Test.Step`:

| Member | Description |
| --- | --- |
| `StepText` | Text including the keyword, for example `Given I have an account`. |
| `Status` | Step status. |
| `Exception` | An `ExceptionWrapper` with type, message and stack trace. |
| `Output` | Console output written during the step and its hooks. |
| `StartTime`, `EndTime`, `TimeTaken` | Timing. |
| `Timeout`, `TimedOut` | Timeout data. |
| `IsRetry` | Whether the step ran in a retry. |
| `Diagnostics` | Messages about step text generation. |
| `Guid`, `ScenarioId`, `ReportId` | Identifiers. |

`StepType` values are `Given`, `AndGiven`, `ButGiven`, `When`, `AndWhen`, `ButWhen`, `Then`, `AndThen` and `ButThen`. `AndWhen` is appended as numeric value `8`; existing numeric values are unchanged. It renders as `And` in reports.

## `BDTestContext`

`BDTestContext` (and `BDTestContext<TContext>` with `TestContext`) describes the running test. It is passed to [`ConfigureContext`](../writing-tests/test-context.md#configuring-a-new-context) and is available on context base classes as `BDTestContext`.

| Member | Description |
| --- | --- |
| `TestContext` | Your context object (generic version only). |
| `TestBase` | The test class instance. |
| `CurrentScenario` | `Scenario?`: the running scenario, or `null` before one exists, including while an outline row is being created. |
| `CurrentScenarioBDTestExecutionId` | The execution ID. |
| `ReportId` | The report ID of the test's run. |
| `GetStoryText()`, `GetScenarioText()` | Story and scenario text. |
