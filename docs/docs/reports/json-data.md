---
title: JSON data
description: Export, read and write BDTest report data as JSON with BDTestJsonHelper.
---

# JSON data

The JSON report is the raw data behind every BDTest report: the same model the report server stores.

## Exporting

```csharp
using BDTest.Helpers;

var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor { Tag = "Nightly" });
File.WriteAllText("BDTest-Report.json", json);
```

| Overload | Use |
| --- | --- |
| `GetTestJsonData()` | Default descriptor. |
| `GetTestJsonData(BDTestRunDescriptor)` | Label the run. |
| `GetTestJsonData(BDTestRunDescriptor, BDTestRun)` | Export an [explicit run](../advanced/explicit-runs.md). |

The output is indented.

## Reading and writing reports

Use the serializer methods to work with `BDTestOutputModel` directly:

```csharp
using BDTest.Helpers;
using BDTest.Maps;

BDTestOutputModel report = BDTestJsonHelper.Deserialize(File.ReadAllText("BDTest-Report.json"));

await using var stream = File.OpenRead("BDTest-Report.json");
BDTestOutputModel fromStream = await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken);

string json = BDTestJsonHelper.Serialize(report, writeIndented: true);
```

| Method | Notes |
| --- | --- |
| `Serialize(BDTestOutputModel model, bool writeIndented = false)` | Writes plain JSON with BDTest's fixed contract. |
| `Deserialize(string json, long maximumExpandedSize = 100 MiB)` | Reads current and historical reports. |
| `DeserializeAsync(Stream stream, CancellationToken cancellationToken = default, long maximumExpandedSize = 100 MiB)` | Reads UTF-8 JSON from a stream and leaves the stream open. |

Both readers accept reports written by older BDTest versions with Newtonsoft.Json, including `$id`/`$ref` references and `$values` wrappers. They reject missing, duplicate or cyclic references. `maximumExpandedSize` limits how large legacy references may expand; a negative value throws `ArgumentOutOfRangeException`.

## The model

`BDTestOutputModel` (namespace `BDTest.Maps`):

| Property | Type | Description |
| --- | --- | --- |
| `Id` | `string` | Report ID. Written as lowercase `id`. |
| `Environment`, `Tag`, `BranchName` | `string` | From the [run descriptor](overview.md#run-descriptors). |
| `MachineName`, `Version` | `string` | Where the tests ran and the BDTest version. |
| `TestTimer` | `TestTimer` | Start, finish and elapsed time of the run. |
| `Scenarios` | `List<Scenario>` | Every executed scenario, including outlines with their rows. |
| `NotRun` | `List<BuildableTest>` | Tests that were built but not executed. |
| `CustomProperties` | `Dictionary<string, string>` | Extra values from the obsolete `BDTestSettings.CustomProperties`. |

### Format details

- Property names match the C# names, apart from `id` at the root.
- Statuses and step types are written as enum names. Historical numeric values remain readable.
- Step, scenario and attempt `TimeTaken` values use the `d.hh:mm:ss:fffffff` format. Nullable timeouts and the timer's `ElapsedTime` use the standard invariant `TimeSpan` format.
- Shared steps, metadata and example values are written as independent values. Do not depend on object identity after deserializing. Outline rows identify their parent through `OutlineId`.
- `Step.Attachments` and `Scenario.Attachments` contain [attachment metadata](../writing-tests/attachments.md) with a one-based `AttemptNumber`. Content and local temporary paths are excluded. Historical reports without these properties read as empty collections; capture starts with a null `Uri`.

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

## Using System.Text.Json directly

`BDTestJsonContext` provides source-generated metadata for the report types. Use its `JsonTypeInfo` properties when calling `System.Text.Json` yourself. Historical reports with references must go through the BDTest readers above. The serializer does not use global Newtonsoft settings or your host's MVC JSON options.

Upgrading from a version that used Newtonsoft.Json? See [JSON serialization changes](../migration/json-serialization.md).
