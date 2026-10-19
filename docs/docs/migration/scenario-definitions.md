---
title: Scenario definitions
description: Migrate pending-report consumers from fluent builders to definition summaries.
---

# Scenario definitions

In the next major version, fluent construction and pending report data use separate types. `BuildableTest` is only a fluent construction base. Internal `ScenarioDefinition` and `StepDefinition` objects retain metadata, delegates, lazy arguments, tags and timeouts. Execution snapshots these definitions and allocates fresh state; results never become the source for a new chain.

## Pending report consumers

`BDTestUtil.GetNotRunScenarios(run)` now returns `IReadOnlyList<BDTest.Contracts.ScenarioDefinitionSummary>`. New `BDTestReport.NotRun` uses the same type; historical `BDTestOutputModel` retains the legacy summary. Replace declarations of `List<BuildableTest>` used for pending report data with `IReadOnlyList<BDTest.Contracts.ScenarioDefinitionSummary>`.

Read `StoryText`, `ScenarioText`, `Parameters`, tags and caller details as before. The summary adds `Timeout` and `Steps`, where each step has `StepType`, template `StepText` and `Timeout`. `ScenarioId` identifies the pending definition and matches the fluent builder's `ScenarioId`. Compare this ID rather than comparing a summary with a builder instance. Summaries have no `BdTestBase` or `Run` reference and cannot be executed or extended.

```csharp sample="NUnit/PendingDefinitions.cs" title="Compiled example"
using BDTest;
using BDTest.Contracts;

public static class PendingDefinitionsExample
{
    public static void Print(BDTestRun run)
    {
        IReadOnlyList<ScenarioDefinitionSummary> pending = BDTestUtil.GetNotRunScenarios(run);
        foreach (var definition in pending)
        {
            Console.WriteLine($"{definition.ScenarioId}: {definition.ScenarioText}");
        }
    }
}
```

Each query returns independent immutable snapshots with read-only collections. Later builder changes do not change earlier snapshots. Schema-versioned pending JSON uses the contract's camel-case field names, including `parameters`; historical unversioned reports without `Steps` or `Timeout` still load through the legacy reader. Step templates are cached during construction and refreshed when `WithArgs` configures explicit readers. Pending queries and exports do not reconstruct custom `StepTextAttribute` instances. Export never evaluates lazy readers, including `WithStepText`, so pending steps show template text rather than callback-generated text.

Pending and executed result `CustomTestInformation` entries contain only strings copied from each attribute's `Information` property. Custom attribute types, additional members and custom type names in `Print()` are not retained in these data-only snapshots. Replace subtype checks with reads of the strings. Builders retain custom attribute instances; snapshots and JSON store only information strings. This avoids retaining user objects or invoking custom constructors during export.

Use `BDTestJsonHelper` to serialize reports. Fluent builder types are no longer part of `BDTestJsonContext`'s report graph. Do not pass a builder to a serializer expecting report data.

## Execution

The `Given(...).When(...).Then(...)` API, direct `await`, `BDTestAsync()`, one execution per builder, retries, hooks, outlines and explicit-run ownership keep their behavior. Execution-step construction copies cached templates and argument readers without reconstructing step-text attributes or evaluating callbacks. Text overrides and argument rendering still run during step execution. Each branch copies definition settings; changes to a prefix after branching do not affect its existing continuations. Completed reports release runtime delegates without preventing a new continuation from using the retained definition.

Execution now returns immutable `BDTest.Contracts` results, and exports and uploads use schema-versioned reports. See [Immutable results](immutable-results.md) for the result API and hosted-server compatibility requirements. Builder namespace cleanup and renderer extraction remain separate changes.
