---
title: Public names and fluent builders
description: Migrate report identity properties, parameter names, builder variables and inline contexts.
---

# Public names and fluent builders

The next major release removes the old members below without obsolete aliases.

| Before | After |
| --- | --- |
| `BDTestUtil.GetCurrentReportId` | `run.ReportId` (the owning run's ID) |
| `BDTestContext.CurrentTestRunnerId` | `BDTestContext.ReportId` (the owning test run's ID) |
| `BuildableTest.Parameters` | `BuildableTest.ParameterNames` (test method parameter names, not values) |

For an explicit run, read `run.ReportId`. The serialized not-run definition field remains `Parameters` so existing report data stays readable. The separate report contract `ScenarioDefinitionSummary.Parameters` is unchanged.

## Fluent builder types

`Given`, `AndGiven`, `ButGiven`, `When`, `AndWhen`, `ButWhen`, `Then`, `AndThen` and `ButThen` are sealed. They no longer inherit from one another. Their names and delegate overloads stay the same.

Use `var` or the specific step kind when retaining a chain for further customization. For example, change `Then checks = builder.Then(Check).But(OtherCheck);` to `var checks = builder.Then(Check).But(OtherCheck);` (its type is `ButThen`). When sharing continuations across kinds, use `GivenStepBuilder`, `WhenStepBuilder` or `ThenStepBuilder` from `BDTest` (see [consumer namespace migration](consumer-namespaces.md)). `RunnableStepBuilder` still provides execution and direct await for any completed chain. These abstract bases do not expose fluent text/argument customization; use a concrete builder for that.

Each concrete builder's `WithStepText` and `WithArgs` returns that same concrete type. `Then`, `AndThen` and `ButThen` also return themselves from `WithScenarioText`, so naming a scenario no longer ends the fluent chain:

```csharp title="Illustrative fragment"
await When(() => Submit())
    .Then(() => CheckResult())
    .WithScenarioText("Successful submission")
    .And(() => CheckAudit());
```

Caller metadata, lazy argument evaluation, independent branches and single execution per builder retain their existing behavior.

## Inline contexts

Both `BDTestBase.WithContext<T>(...)` overloads are removed. Construct an inline context explicitly:

```csharp title="Illustrative fragment"
var context = new MyContext();
await Given(() => Prepare(context))
    .When(() => Act(context))
    .Then(() => Verify(context));
```

You own this context and its disposal. For framework-managed initialization, retry recreation and disposal, use a [context base class](../writing-tests/test-context.md) or the [composition host](../frameworks/composition.md).
