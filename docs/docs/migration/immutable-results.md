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
accept `BDTest.Test` structured arguments; result arguments use the contracts types.

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

Hosted migration is tracked in [#936](https://github.com/thomhurst/BDTest/issues/936).
The current legacy host cannot consume the new uploads. Export JSON or static HTML
locally until using a server that explicitly supports schema version 1; keep client
and server package generations aligned. Renderer extraction remains separate in
[#851](https://github.com/thomhurst/BDTest/issues/851).
