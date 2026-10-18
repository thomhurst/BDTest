---
title: Exception classification
description: How BDTest maps exceptions to step and scenario statuses, and how to register success, skip and inconclusive exception types.
---

# Exception classification

When a step throws, BDTest picks a status from the exception type:

| Exception | Step and scenario status | Retried? |
| --- | --- | --- |
| `NotImplementedException` | `NotImplemented` | No |
| A registered **success** type | `Passed` | No |
| A registered **skip** type | `Skipped`; remaining steps are also `Skipped` | No |
| A registered **inconclusive** type | `Inconclusive` | No |
| A [timeout](../writing-tests/timeouts.md) | `Failed`, with `TimedOut = true` | Yes, if configured |
| Anything else | `Failed` | Yes, if configured |

In every case BDTest records the exception and rethrows it, so your test runner sees the original outcome. For example, TUnit's `Skip.Test(...)` throws `SkipTestException`: BDTest records `Skipped` and TUnit reports a skipped test. NUnit's `Assert.Pass()` throws `SuccessException`: BDTest records `Passed` and NUnit reports a pass.

Exceptions raised because a timeout expired are always failures, even if their type is registered.

## Registering exception types

`BDTestSettings.CustomExceptionSettings` holds three thread-safe collections:

```csharp
using BDTest.Settings;

BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(MyPassException));
BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(MySkipException));
BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
```

- Matching uses the **exact** exception type. Register each concrete type you need; base types do not match derived exceptions.
- Register types once, during test-run setup. The collections are `ConcurrentBag<Type>`, so check `Contains` first if setup can run more than once.

## Built-in registrations

The framework adapters register their own types when they first run:

| Adapter | Success | Skip | Inconclusive |
| --- | --- | --- | --- |
| TUnit | — | `SkipTestException` | `InconclusiveTestException` |
| NUnit | `SuccessException` | `IgnoreException` | `InconclusiveException` |
| MSTest | — | — | `AssertInconclusiveException` |
| xUnit | — | `SkipException` | — |

Standalone use registers nothing.

## Skip versus inconclusive

Use a **skip** type for "this scenario intentionally did not run here". Skips are reported as `Skipped` and are never retried. Use an **inconclusive** type for "this scenario ran but could not decide". See [Skipping](../writing-tests/skipping.md) and [Statuses](statuses.md).
