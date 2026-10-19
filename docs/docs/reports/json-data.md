---
title: JSON data
description: Export, read and write BDTest report data as JSON with BDTestJsonHelper.
---

# JSON data

For built-in adapters, pass the [configured assembly run](../configuration/assembly-runs.md) to report/utility helpers. Every export and store utility requires the owning run. Export all reports before disposing through the assembly owner.


Core exports and uploads are immutable [versioned reports](contracts.md): camelCase
properties, `schemaVersion: 1`, and a separate `producerVersion`. Status and step
type values are names; timestamps are UTC `DateTimeOffset` values and durations
use invariant `TimeSpan` format. All collections are read-only and non-null.

Read a new export with `BDTestJsonHelper.DeserializeReport(json)` (returns
`BDTest.Contracts.BDTestReport`), and write it with `BDTestJsonHelper.Serialize(report)`.
`BDTestReportJson` in the contracts package provides the same schema without a
runtime dependency. Missing or unsupported schema versions are rejected.

Legacy hosted readers and storage still use the unversioned model described
below, pending [#936](https://github.com/thomhurst/BDTest/issues/936). Do not upload
new reports to those hosts. See [migration](../migration/immutable-results.md).

## Exporting

```csharp title="Illustrative fragment"
using BDTest.Helpers;

var run = BDTest.NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor { Tag = "Nightly" }, run);
File.WriteAllText("BDTest-Report.json", json);
```

| Overload | Use |
| --- | --- |
| `GetTestJsonData(BDTestRunDescriptor, BDTestRun)` | Export an [explicit run](../advanced/explicit-runs.md). |

The output is indented.

## Reading and writing legacy reports

For historical unversioned data, use the legacy serializer methods to work with `BDTestOutputModel` directly:

```csharp title="Illustrative fragment"
using BDTest.Helpers;
using BDTest.Maps;

BDTestOutputModel report = BDTestJsonHelper.Deserialize(File.ReadAllText("BDTest-Report.json"))
    ?? throw new InvalidDataException("Expected report data.");

await using var stream = File.OpenRead("BDTest-Report.json");
BDTestOutputModel fromStream = await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken)
    ?? throw new InvalidDataException("Expected report data.");

string json = BDTestJsonHelper.Serialize(report, writeIndented: true);
```

| Method | Notes |
| --- | --- |
| `Serialize(BDTestOutputModel? model, bool writeIndented = false)` | Writes plain JSON with BDTest's fixed contract; a null model writes `null`. |
| `Deserialize(string json, long maximumExpandedSize = 100 MiB)` | Reads unversioned historical reports. |
| `DeserializeAsync(Stream stream, CancellationToken cancellationToken = default, long maximumExpandedSize = 100 MiB)` | Reads UTF-8 JSON from a stream and leaves the stream open. |

Both readers accept reports written by older BDTest versions with Newtonsoft.Json, including `$id`/`$ref` references and `$values` wrappers. They reject missing, duplicate or cyclic references. `maximumExpandedSize` limits how large legacy references may expand; a negative value throws `ArgumentOutOfRangeException`.

Both readers return a nullable model: the JSON literal `null` produces `null`. Handle that result before accessing report members.

## The legacy model

`BDTestOutputModel` (namespace `BDTest.Maps`):

| Property | Type | Description |
| --- | --- | --- |
| `Id` | `string?` | Report ID. Written as lowercase `id`. |
| `Environment`, `Tag`, `BranchName` | `string?` | From the [run descriptor](overview.md#run-descriptors). |
| `MachineName`, `Version` | `string?` | Where the tests ran and the BDTest version. |
| `TestTimer` | `TestTimer?` | Start, finish and elapsed time of the run. |
| `Scenarios` | `List<Scenario>?` | Every executed scenario, including outlines with their rows. |
| `NotRun` | `List<ScenarioDefinitionSummary>?` | Tests that were built but not executed. |
| `CustomProperties` | `Dictionary<string, string>?` | Extra values copied from `BDTestRunDescriptor.CustomProperties`. |

### Pending scenarios

`NotRun` contains dedicated `ScenarioDefinitionSummary` values rather than builders. Existing scenario/story text, metadata, caller and parameter fields remain readable, including historical reports without step summaries. New summaries also include the scenario `Timeout` and `Steps` (`StepType`, template `StepText`, `Timeout`). They contain no fixture, delegates, run object, output or execution state. Export does not evaluate lazy argument or text readers.

### Format details

- Property names match the C# names, apart from `id` at the root.
- Statuses and step types are written as enum names. Historical numeric values remain readable.
- Step, scenario and attempt `TimeTaken` values use the `d.hh:mm:ss:fffffff` format. Nullable timeouts and the timer's `ElapsedTime` use the standard invariant `TimeSpan` format.
- Shared steps, metadata and example values are written as independent values. Do not depend on object identity after deserializing. Outline rows identify their parent through `OutlineId`.
- `Step.Attachments` and `Scenario.Attachments` contain [attachment metadata](../writing-tests/attachments.md) with a one-based `AttemptNumber`. Content and local temporary paths are excluded. Historical reports without these properties read as empty collections; capture starts with a null `Uri`.

### Nullable report fields

Nullable reference annotations describe fields that can already be absent during execution or when reading historical JSON. They do not add JSON properties, change property names or require older reports to supply missing values.

`Scenario.Exception` and `Step.Exception` are nullable when no exception was captured. Scenario and step identifiers, output, source filenames and `Step.StepText` can also be null in incomplete or historical records. `Scenario.OutlineId` is null for a top-level scenario. Optional scenario HTML and test information remain nullable.

Report collections use empty defaults, but historical or custom JSON can explicitly set them to `null`. Their nullable annotations preserve that accepted input. Check collections before enumerating them, including root `Scenarios` and `NotRun`, scenario steps, examples and attempts, and exception data and children. For example, enumerate `report.Scenarios ?? Enumerable.Empty<Scenario>()` when an absent collection should count as empty.

`ExceptionWrapper.Message`, `StackTrace`, `TypeName`, `ShortTypeName`, `Source`, `AsString` and `TruncationReason` are nullable. For example, an exception that has not been thrown has no stack trace, and an older report may not include structured exception fields. Custom report code must check optional values before using string members:

```csharp title="Illustrative fragment"
var title = step.StepText ?? "Unnamed step";
if (step.Exception?.StackTrace is { } stackTrace)
{
    Console.WriteLine(stackTrace);
}
```

Runtime references are separate from report data: `Scenario.Run` is null after deserialization, and `Scenario.BdTestBaseClass` is null when no live fixture is available. Both are excluded from JSON.

### Structured step arguments

`Step.Arguments` is an `IReadOnlyList<StepArgument>` containing [tables and doc strings](../writing-tests/step-text.md#tables-and-doc-strings) captured for the final execution attempt. Older reports without this field load with an empty list. Outline example steps retain their own argument values.

The source-generated `BDTestJsonContext` registers `StepArgument`, `StepTable` and `StepDocString`. Each argument has a `$type` discriminator:

```json
{
  "Arguments": [
    { "$type": "table", "Headers": ["Name"], "Rows": [["Alice"], ["Bob"]] },
    { "$type": "docString", "Content": "{\n  \"active\": true\n}", "ContentType": "application/json" }
  ]
}
```

Table cells are strings, with null cells normalized to empty strings when creating a table. `ContentType` is optional. HTML encoding occurs during rendering; JSON preserves the complete values, including rows hidden by the HTML preview. The same contract is used for report uploads.

### Structured exceptions

`Step.Exception`, `Scenario.ExecutionException` and retry-attempt `Exception` values contain an `ExceptionWrapper`. `TypeName` is the full type name; `ShortTypeName` is for display. Each node includes `Message`, `StackTrace`, `Source`, nullable `HResult`, a string dictionary `Data`, and an `InnerExceptions` list. Aggregates retain every child. `IsWrapper` marks invocation wrappers and single-child aggregates without removing their diagnostics.

Capture has a ten-node depth limit and a cycle guard. `TruncationReason` records `DepthLimit` or `Cycle` when traversal stops. `AsString` retains original diagnostic text for ordinary graphs and bounded text for truncated graphs. Historical JSON without these fields still loads. See [structured exception migration](../migration/structured-exceptions.md) for grouping, data formatting and compatibility details.

## Using System.Text.Json directly

`BDTestJsonContext` provides source-generated metadata for the report types. Use its `JsonTypeInfo` properties when calling `System.Text.Json` yourself. Historical reports with references must go through the BDTest readers above. The serializer does not use global Newtonsoft settings or your host's MVC JSON options.

Upgrading from a version that used Newtonsoft.Json? See [JSON serialization changes](../migration/json-serialization.md).

### Step skip reasons

`Step.SkipReason` is optional. It records an intentional step condition skip as
`{ "Source": "Rule", "AttributeType": "MyTests.SkipIfAttribute", "RuleIndex": 0 }`
or `{ "Source": "DebugSetting", "Setting": "Debug.ShouldSkipWhenStep" }`.
Unused fields can be null. `RuleIndex` is zero-based within the registration
snapshot used by that attempt; attributes and predicate delegates are never
serialized. Static HTML and hosted reports display the reason as encoded text.

Older reports without `SkipReason` deserialize with a null reason. Absence does
not imply that a step ran: historical, scenario-level and exception-based skips
can have no condition reason. See [skip behavior](../writing-tests/skipping.md).
