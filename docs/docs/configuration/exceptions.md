---
title: Exception classification
description: How BDTest maps exceptions to step and scenario statuses, including inheritance, wrappers and per-run registrations.
---

# Exception classification

Steps, scenarios and scenario outline examples share one exception classifier. When a step throws, BDTest picks a status from the exception:

| Exception | Step and scenario status | Retried? |
| --- | --- | --- |
| `NotImplementedException`, including subclasses | `NotImplemented` | No |
| A registered **success** type | `Passed` | No |
| A registered **skip** type | `Skipped`; remaining unexecuted steps are also `Skipped` | No |
| A registered **inconclusive** type | `Inconclusive` | No |
| A [timeout](../writing-tests/timeouts.md) | `Failed`, with `TimedOut = true` | Yes, if configured |
| Anything else | `Failed` | Yes, if configured |

The rows are checked in this order. Only failed outcomes are eligible for [retries](../writing-tests/retries.md). A skip keeps the statuses of steps that already ran.

In every case BDTest records the exception and rethrows it, so your test runner sees the original outcome. For example, TUnit's `Skip.Test(...)` throws `SkipTestException`: BDTest records `Skipped` and TUnit reports a skipped test. NUnit's `Assert.Pass()` throws `SuccessException`: BDTest records `Passed` and NUnit reports a pass.

Timeouts always produce `Failed`, even when `TimeoutException` is registered.

## Inheritance and wrappers

- A registered type matches that type **and its subclasses**.
- Before matching, BDTest unwraps `TargetInvocationException` and `AggregateException` with exactly one inner exception, recursively.
- An `AggregateException` with several inner exceptions, or none, stays intact. It is `Failed` unless the aggregate type or a base type is registered. BDTest never picks one inner exception and hides the others.
- Classification does not replace the exception. Propagation and failure diagnostics keep the wrappers and their inner exceptions.

## Registering exception types

For the default run, which the framework adapters use, add types to `BDTestSettings.CustomExceptionSettings`:

```csharp
using BDTest.Settings;

BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(MyPassException));
BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(MySkipException));
BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
```

These collections are thread-safe `ConcurrentBag<Type>`s and are read live. Register types once, during test-run setup, and check `Contains` first if setup can run more than once.

For an [explicit run](../advanced/explicit-runs.md), register types on `BDTestOptions` before starting it:

```csharp
var options = new BDTestOptions();
options.SuccessExceptionTypes.Add(typeof(MySuccessException));
options.SkipExceptionTypes.Add(typeof(MySkipException));
options.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
using var run = BDTestRun.Start(options);
```

`Start(options)` freezes these registrations; later changes affect only new runs. `Start()` has no registrations, and explicit runs do not read `BDTestSettings`.

## Built-in registrations

The framework adapters register their own types in `BDTestSettings.CustomExceptionSettings` when they first run:

| Adapter | Success | Skip | Inconclusive |
| --- | --- | --- | --- |
| TUnit | — | `SkipTestException` | `InconclusiveTestException` |
| NUnit | `SuccessException` | `IgnoreException` | `InconclusiveException` |
| MSTest | — | — | `AssertInconclusiveException` |
| xUnit | — | `SkipException` | — |

Standalone use registers nothing.

## Skip versus inconclusive

Use a **skip** type for "this scenario intentionally did not run here". Skips are reported as `Skipped` and are never retried. Use an **inconclusive** type for "this scenario ran but could not decide". See [Skipping](../writing-tests/skipping.md) and [Statuses](statuses.md).
