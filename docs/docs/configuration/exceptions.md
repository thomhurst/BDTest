---
title: Exception classification
description: Exception outcomes, inheritance, wrappers, and per-run registrations.
---

# Exception classification

Steps, scenarios, and scenario outline examples use the same exception classifier.
Register success, skip, and inconclusive exception types before starting an explicit run:

```csharp
var options = new BDTestOptions();
options.SuccessExceptionTypes.Add(typeof(MySuccessException));
options.SkipExceptionTypes.Add(typeof(MySkipException));
options.InconclusiveExceptionTypes.Add(typeof(MyInconclusiveException));
using var run = BDTestRun.Start(options);
```

Override `BDTestBase.TestRun` to select that run. `Start(options)` freezes these
registrations; later changes affect only new runs. `Start()` has no registrations.
Existing adapters using the legacy default run read the live collections on
`BDTestSettings.CustomExceptionSettings` instead.

Registered types match both the registered type and its subclasses. Classification
recursively unwraps `TargetInvocationException` and `AggregateException` with exactly
one inner exception. Empty aggregates and aggregates with multiple inner exceptions
stay intact. They fail unless the aggregate type or a base type is registered;
BDTest never selects one inner exception and hides the others.

After unwrapping, classification uses this precedence:

1. `NotImplementedException`, including subclasses: `NotImplemented`.
2. A registered success type: `Passed`.
3. A registered skip type: `Skipped`.
4. A registered inconclusive type: `Inconclusive`.
5. Any other exception: `Failed`.

Timeouts always produce `Failed`, even when `TimeoutException` is registered.
Only failed outcomes are eligible for configured retries. A skip marks remaining
unexecuted steps as `Skipped` and preserves previously executed step statuses.
Classification preserves the original exception and its wrappers for propagation
and failure diagnostics. A successful outline example does not fail its outline.
