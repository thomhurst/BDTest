---
title: Choosing a test framework
description: Compare the BDTest adapters for TUnit, NUnit, xUnit and MSTest, or run BDTest standalone.
---

# Choosing a test framework

BDTest does not replace your test runner. It runs inside it. An adapter package gives you a base class that connects BDTest to the runner's test identity, lifecycle and skip/inconclusive exceptions.

| Adapter | Base class | Package |
| --- | --- | --- |
| [TUnit](tunit.md) (recommended) | `TUnitBDTestBase<TContext>` | `BDTest.TUnit` |
| [NUnit](nunit.md) | `NUnitBDTestBase<TContext>` | `BDTest.NUnit` |
| [xUnit v3](xunit.md) | `XUnitBDTestBase<TContext>` | `BDTest.xUnit` |
| [MSTest](mstest.md) | `MSTestBDTestBase<TContext>` | `BDTest.MSTest` |
| [Standalone](standalone.md) | `BDTestBase`, `BDTestBuilder` | `BDTest` |

All adapter base classes take a [context](../writing-tests/test-context.md) type with the constraint `where TContext : class, new()`. There are no non-generic adapter base classes. Use an empty context class if you do not need state.

If your test class already has another base class, the core [composition host](composition.md) provides a typed context, scenario builders, and output helpers with manual lifecycle integration.

## What an adapter does

Each adapter is a thin class that derives from `AbstractContextBDTestBase<TContext>`. It:

1. supplies a unique execution ID for each test from the framework's test context, so context and output stay isolated in parallel runs,
2. declares the framework's success, skip and inconclusive exceptions in one [`FrameworkOutcomeMap`](../configuration/exceptions.md#framework-outcome-maps), registered idempotently for the default run,
3. marks each test as started and completed, and removes its context afterwards,
4. maps a whole-scenario [skip](../writing-tests/skipping.md#test-runner-outcome) onto the runner's skip mechanism.

## Feature comparison

| | TUnit | NUnit | xUnit v3 | MSTest |
| --- | --- | --- | --- | --- |
| Execution ID | `TestContext.Current.Id` | `Test.ID` + repeat count | `TestContext.Current.Test.UniqueID` | class and data-row display name + execution suffix |
| Success exception | — | `SuccessException` (`Assert.Pass`) | — | — |
| Skip exception | `SkipTestException` (`Skip.Test`) | `IgnoreException` (`Assert.Ignore`) | `SkipException` (`Assert.Skip`) | — |
| Inconclusive exception | `InconclusiveTestException` | `InconclusiveException` | — | `AssertInconclusiveException` |
| Whole-scenario skip | `SkipTestException` | `Assert.Ignore` | `Assert.Skip` | `Assert.Inconclusive` |
| Runs with | `dotnet run` / `dotnet test` (Microsoft.Testing.Platform) | `dotnet test` | `dotnet test` | `dotnet test` |

The examples in this documentation use TUnit. Swap the base class, test attributes and assertions for your framework.

Not using one of these frameworks? See [Custom adapters](custom-adapters.md).

For an explicit run, register the adapter's `OutcomeMap` on `BDTestOptions` before
calling `BDTestRun.Start(options)`. Both classification and skip notifications use
that run's frozen configuration. The [shared conformance suite](../configuration/exceptions.md#runner-differences)
checks each adapter's BDTest statuses and actual runner outcomes, including the
frameworks' explicit differences.
