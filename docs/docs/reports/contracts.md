---
title: Versioned report contracts
description: Immutable report DTOs in BDTest.Contracts, independent of execution.
---

# Versioned report contracts

`BDTest.Contracts` targets `netstandard2.0` and contains immutable report DTOs,
`Status`, `StepType`, and source-generated System.Text.Json metadata. It has no
dependency on `BDTest`, test adapters, test instances or executable delegates.

```bash
dotnet add package BDTest.Contracts
```

Core execution now publishes these snapshots through `BDTestAsync()`, `BDTestUtil`,
run results and hooks. Core JSON export, static HTML and uploads consume the same
contract. Hosted reports and the Azure Blob store also consume these contracts,
without a dependency on the execution package. Hosts read both schema version 1
and historical unversioned reports. See [migration](../migration/immutable-results.md).

## Creating and reading a report

```csharp sample="Contracts/Program.cs" title="Compiled example"
using BDTest.Contracts;

var step = new StepResult(StepType.Given, "Given an account", Status.Passed);
var scenario = new ScenarioResult(
    "scenario-id", "report-id", "Accounts", "Create an account", Status.Passed,
    steps: new[] { step });
var report = new BDTestReport(
    BDTestReport.CurrentSchemaVersion, "report-id", "3.0.0",
    scenarios: new[] { scenario });

string json = BDTestReportJson.Serialize(report);
BDTestReport restored = BDTestReportJson.Deserialize(json);
```

`schemaVersion` is the integer JSON shape version, currently **1**. It changes
only when the report contract changes. `producerVersion` identifies the full
producer release and revision; it does not select a schema. The current-schema
reader rejects missing, non-integer and unsupported schema versions with
`JsonException`. Unversioned historical reports must continue through the
[existing legacy reader](json-data.md#reading-and-writing-legacy-reports).

The schema uses camelCase property names and named status and step type values.
JSON readers reject numeric enum values, including numeric strings, and unknown
names. Constructors reject undefined `Status` and `StepType` values.
Timestamps use `DateTimeOffset`, preserving offsets. Durations use the standard
invariant System.Text.Json `TimeSpan` format, including fractional seconds;
they do not use the legacy `d.hh:mm:ss:fffffff` duration format.
`BDTestReportJsonContext.Default.BDTestReport` also supplies generated metadata
for direct System.Text.Json calls. Direct deserialization with that metadata
requires a supported constructor schema version; use `BDTestReportJson` for
explicit version dispatch and `JsonException` diagnostics.

## Immutable snapshots

Every data type has get-only properties. Constructors copy every collection
input into read-only storage, including dictionaries and nested table rows.
Changing a source list after construction cannot change a report. Collection
properties accept null during construction/deserialization and become empty
collections, except `StepTable.Headers` and `StepTable.Rows`, which require
non-null inputs. Collection entries must be non-null, except table headers and cells,
where null becomes an empty string. Null or incorrectly sized table rows are
rejected. Missing required strings are rejected; optional diagnostic and host
metadata strings remain nullable.

| Type | Contents |
| --- | --- |
| `BDTestReport` | Schema/producer versions, report ID, descriptor, timer, scenarios, pending definitions and custom properties. |
| `ScenarioResult` | Text, status, final steps, attempts, outlines/examples, tags, information, exception, outputs, attachments and timing. |
| `StepResult` | Public `StepType`, text, status, retry flag, exception, output, arguments, diagnostics, attachments and timing. |
| `ScenarioAttemptResult` | Attempt number, status, exception, timing and optional step snapshots. |
| `ScenarioDefinitionSummary` | Pending definition metadata without a builder or execution state. |
| `ExceptionInfo` | Full `TypeName`, display `ShortTypeName`, message, stack trace, original text, Data, HResult, Source, children, wrapper flag and truncation reason. |
| `AttachmentInfo` | File ID, name, MIME type, byte length, attempt number and optional remote URI. No local path or content. |
| `StepTable`, `StepDocString` | Structured arguments using `$type` values `table` and `docString`. Tables snapshot all rows. |
| `ReportTimer` | Run start/finish timestamps and their calendar span. |

Exception nodes preserve the shape of [structured exceptions](../migration/structured-exceptions.md),
including every aggregate branch. `TypeName` carries the full identity and
`ShortTypeName` is for display; when omitted, the latter derives from `TypeName`.
DTOs hold already captured data and never inspect live exceptions or apply
execution outcome rules. Producers remain responsible for bounded capture.

Scenario and step `TimeTaken` values are measured execution durations.
`ReportTimer.ElapsedTime` is the calendar difference, or zero when either
endpoint is absent. `ScenarioResult.TimedOut` derives from its final steps.
Attempts can retain separate step snapshots; legacy attempt data without them
maps to an empty list. `CustomTestInformation` contains strings, matching the
legacy attribute wire values. Tags retain producer order.

`AttachmentUploadTokenHash` is optional server-managed storage metadata. It
never contains the plaintext upload token. Preserve existing upload authorization
when adapting this DTO to storage.

`StepResult.SkipReason` optionally carries a `StepSkipReason` with `Source`,
`AttributeType`, `RuleIndex` and `Setting`, using the same meanings as
[step condition reasons](../writing-tests/skipping.md). JSON uses camelCase and
omits `skipReason` when null. This additive optional field keeps schema version 1;
existing version-1 reports without it remain readable.
