---
title: Immutable results and versioned JSON
description: Migrate execution results, hooks and report consumers to BDTest.Contracts.
---

# Immutable results and versioned JSON

This major-version change separates mutable execution from immutable report data.

| Previous API | Replacement |
| --- | --- |
| `Task<BDTest.Test.Scenario>` from fluent builders/outlines | `Task<BDTest.Contracts.ScenarioResult>` |
| `List<Scenario>` from `BDTestUtil.GetScenarios(run)` | `IReadOnlyList<ScenarioResult>` |
| Pending `BDTest.Test.ScenarioDefinitionSummary` | `BDTest.Contracts.ScenarioDefinitionSummary` |
| Step hook `Step` argument | `BDTest.Contracts.StepResult` |
| `ScenarioHookContext.Scenario` live scenario | Immutable `ScenarioResult` |
| `BDTest.Test.Status`, `BDTest.Test.Steps.StepType` | `BDTest.Contracts.Status`, `BDTest.Contracts.StepType` |
| Result `GetStoryText()` / `GetScenarioText()` | `StoryText` / `ScenarioText` properties |
| Result `TestInformationAttribute` objects | Information strings |
| Result `DateTime` timestamps | UTC `DateTimeOffset` timestamps |

Add `using BDTest.Contracts` or explicit aliases when both execution argument
builders and contract `StepTable`/`StepDocString` types are used. Builders still
accept structured arguments from `BDTest`; result arguments use the contracts types.
See [consumer namespaces](consumer-namespaces.md) for the namespace migration.

Results contain no fixture, execution delegate or run reference. Nested lists,
dictionaries, tables and exception trees are immutable. Query `run.Scenarios` again
after teardown to obtain later output. Earlier snapshots remain unchanged after
retry, clear or disposal. Use test context and the attachment/HTML writers for
recording evidence, rather than modifying result collections.

Before/after hook arguments are separate snapshots. Step after snapshots include
step execution timing; scenario after snapshots precede finalized attempt timing
and history. See [step hooks](../writing-tests/step-hooks.md) and
[scenario hooks](../writing-tests/scenario-hooks.md).

Core JSON export, static HTML and upload share `BDTestReport`. New JSON has
camelCase fields, independent `schemaVersion` and `producerVersion`, enum names,
standard invariant durations, and UTC timestamps. Use
`BDTestJsonHelper.DeserializeReport` or `BDTestReportJson.Deserialize` for new
reports. The old `Deserialize`/`DeserializeAsync` APIs remain for historical
unversioned data; they do not provide a versioned-to-legacy conversion.

`BDTestJsonHelper.Serialize(report)` writes compact JSON for a `BDTestReport`.
Use `Serialize(report, writeIndented: true)` for formatted JSON. These are separate
one- and two-argument overloads: the formatting parameter is
required when selecting the two-argument method. Existing calls keep the same
syntax and output. The legacy `BDTestOutputModel` serializer uses the same
overload pattern and still accepts null. Reflection-based callers should select
the one-argument overload when omitting formatting; neither two-argument method
declares an optional default.

## Hosted reports

Upgrade hosts and clients together for schema-version-1 uploads. `BDTest.Razor.Reports`
and `BDTest.Reports.Storage.AzureBlob` reference `BDTest.Contracts`, not execution.
Existing persisted unversioned reports remain readable without a data migration.

Change `IBDTestDataStore`, `IDataRepository`, `IBDTestDataReceiver` and
`IBDTestCustomSidebarLinksProvider` implementations from `BDTestOutputModel` to
`BDTestReport`. Model reads remain nullable. The legacy model-store adapter still
forwards JSON `null` to `StoreTestData`; modern byte stores retain raw JSON.
Use `HostedReportJson` for version-dispatched reads and `BDTestReportJson` for writes.

Update copied Razor views to `ScenarioResult`, `StepResult`, `ExceptionInfo` and
`AttachmentInfo`; use text properties and information strings. Full report data is
in `ViewData[nameof(BDTestReport)]`. `ReportLayoutViewModel.Timer` is `ReportTimer`.
The compact summary models and existing summary/index JSON keep their names.

Run summaries normalize timestamp offsets to UTC so history ordering, date filters
and durations compare instants. Scenario and outline pages display shared failure
details once even after a JSON round trip; exceptions with different diagnostic
data or inner exceptions remain separate.

Contract properties are read-only. Construct a new `BDTestReport` when changing
metadata. The upload controller assigns a fresh storage ID and attachment-token
hash to a new report, ignoring uploader-supplied identity/credentials, before
calling storage and receivers. Authorization, attachment limits and URLs are unchanged.
Renderer extraction remains separate in [#851](https://github.com/thomhurst/BDTest/issues/851).

