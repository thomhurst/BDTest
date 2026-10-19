---
title: Exception classification
description: How BDTest maps exceptions to step and scenario statuses, including inheritance, wrappers and per-run registrations.
---

# Exception classification

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


Steps, scenarios and scenario outline examples share one exception classifier. When a step throws, BDTest picks a status from the exception:

| Exception | Step and scenario status | Retried? |
| --- | --- | --- |
| `NotImplementedException`, including subclasses | `NotImplemented` | No |
| A registered **success** type | `Passed` | No |
| A registered **skip** type | `Skipped`; remaining unexecuted steps are also `Skipped` | No |
| A registered **inconclusive** type | `Inconclusive` | No |
| A [timeout](../writing-tests/timeouts.md) | `Failed`, with `TimedOut = true` | If configured and cancelled work settles |
| Explicit or adapter cancellation | `Failed`, with an `OperationCanceledException` and `TimedOut = false` | No |
| Anything else | `Failed` | Yes, if configured |

Execution cancellation and timeouts take precedence over registered exception types. For ordinary exceptions, the other rows are checked in the order shown. Only failed outcomes are eligible for [retries](../writing-tests/retries.md). A skip keeps the statuses of steps that already ran.

In every case BDTest records the classification and rethrows the original exception, so your test runner applies its own outcome rules. For example, TUnit's `Skip.Test(...)` throws `SkipTestException`: BDTest records `Skipped` and TUnit reports a skipped test. NUnit's `Assert.Pass()` throws `SuccessException`: BDTest records `Passed` and NUnit reports a pass.

Timeouts and explicit or adapter cancellation always produce `Failed`, even when their exception types are registered. An unrelated user-thrown `OperationCanceledException` follows the ordinary classification rules.

## Inheritance and wrappers

- A registered type matches that type **and its subclasses**.
- Before matching, BDTest unwraps `TargetInvocationException` and `AggregateException` with exactly one inner exception, recursively.
- An `AggregateException` with several inner exceptions, or none, stays intact. It is `Failed` unless the aggregate type or a base type is registered. BDTest never picks one inner exception and hides the others.
- Classification does not replace the exception. Propagation and failure diagnostics keep the wrappers and their inner exceptions.

## Registering exception types

For built-in adapters, add custom types in `IBDTestConfiguration.Configure` for
[the test assembly](assembly-runs.md):

```csharp title="Illustrative fragment"
public sealed class TestConfiguration : IBDTestConfiguration
{
    public void Configure(BDTestOptions options)
    {
        options.SuccessExceptionTypes.Add(typeof(MyPassException));
        options.SkipExceptionTypes.Add(typeof(MySkipException));
        options.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
    }
}
```

The assembly run freezes these registrations when it is first created. Each run owns its exception registrations.

For an [explicit run](../advanced/explicit-runs.md), register types on `BDTestOptions` before starting it:

```csharp title="Illustrative fragment"
var options = new BDTestOptions();
options.SuccessExceptionTypes.Add(typeof(MySuccessException));
options.SkipExceptionTypes.Add(typeof(MySkipException));
options.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
using var run = BDTestRun.Start(options);
```

`Start(options)` freezes these registrations; later changes affect only new runs. `Start()` has no registrations.

## Built-in registrations

The framework adapters register their own types on their assembly run during initialization, before its options are frozen:

| Adapter | Success | Skip | Inconclusive |
| --- | --- | --- | --- |
| TUnit | — | `SkipTestException` | `InconclusiveTestException` |
| NUnit | `SuccessException` | `IgnoreException` | `InconclusiveException` |
| MSTest | — | — | `AssertInconclusiveException` |
| xUnit | — | `SkipException` | — |

Standalone use registers nothing.

## Skip versus inconclusive

Use a **skip** type for "this scenario intentionally did not run here". Skips are reported as `Skipped` and are never retried. Use an **inconclusive** type for "this scenario ran but could not decide". See [Skipping](../writing-tests/skipping.md) and [Statuses](statuses.md).

## Framework outcome maps

Each built-in adapter exposes an immutable `OutcomeMap` of type
`BDTest.Settings.FrameworkOutcomeMap`. Register that declaration when selecting
an explicit run, so classification and runner notifications belong to that run:

```csharp title="Illustrative fragment"
var options = new BDTestOptions();
options.RegisterFrameworkOutcomes(NUnitBDTestBase<object>.OutcomeMap);
using var run = BDTestRun.Start(options);
```

Use the matching base class for MSTest, TUnit, or xUnit. Re-registering a map adds
no duplicate exception types. Registration preserves existing custom types. If
several maps are registered, their exception types accumulate and the last map
supplies the runner notifications. `Start(options)` freezes both the exception
types and the selected notifications. Explicit runs never read adapter mappings
from global settings. `Start()` still starts with no mappings.

For a custom adapter, declare a map once:

```csharp title="Illustrative fragment"
var map = new FrameworkOutcomeMap(
        reason => Runner.Skip(reason),
        reason => Runner.Inconclusive(reason))
    .Success<MySuccessException>()
    .Skip<MySkipException>()
    .Inconclusive<MyInconclusiveException>();
options.RegisterFrameworkOutcomes(map);
```

Every fluent method returns a new map; retain its return value. `ReportSkip(reason)`
and `ReportInconclusive(reason)` invoke the declared runner callbacks. An omitted
inconclusive callback falls back to the skip callback. BDTest invokes the owning
explicit run's skip callback after finalizing a wholly skipped scenario or outline.
Framework outcome exceptions thrown inside steps retain their original propagation.

Built-in adapters register their declarations on their assembly run's options.
### Runner differences

The shared suite in `TestUtilities/AdapterConformanceTests.cs` runs in all four
adapter test projects. CI runs `scripts/Test-AdapterConformance.ps1` to compare
BDTest statuses with actual TRX outcomes. It also tests concurrent explicit runs
with different frozen mappings. Intentional failures are enabled only in that
script; normal test runs catch and verify those exceptions.

| Adapter | Success exception | Framework skip | Framework inconclusive | Wholly skipped scenario |
| --- | --- | --- | --- | --- |
| NUnit | `SuccessException`: Passed | `IgnoreException`: Skipped | `InconclusiveException`: Inconclusive | `Assert.Ignore` |
| MSTest | None; normal completion | Uses `AssertInconclusiveException`: Inconclusive | `AssertInconclusiveException`: Inconclusive | `Assert.Inconclusive` |
| TUnit | None; normal completion | `SkipTestException`: Skipped | `InconclusiveTestException`: Inconclusive | `SkipTestException` |
| xUnit v3 | None; normal completion | `SkipException`: Skipped | Falls back to skip: Skipped | `Assert.Skip` |

MSTest 4.5.1 has no separate runtime skip assertion. Its inconclusive results map
to `NotExecuted` in default TRX output, as do NUnit inconclusive results.
TUnit 1.75.0 reports its native `InconclusiveTestException` as **Failed** in TRX,
while BDTest records **Inconclusive**. The suite asserts that difference explicitly.
xUnit v3 supports runtime skip but has no distinct inconclusive or success exception.
Unmapped failures and `NotImplementedException` produce runner failures; BDTest
records them as `Failed` and `NotImplemented`, respectively.
