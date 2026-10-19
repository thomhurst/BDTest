---
title: Consumer namespaces
description: Use one BDTest namespace for test bases, fluent builders and attributes.
---

# Consumer namespaces

The next major version moves test-authoring APIs into `BDTest`. Replace the
imports below with `using BDTest;` and rebuild your tests and custom adapters.
This is a source and binary breaking change; the old fully qualified type names
are not compatibility aliases.

| Previous namespace | Types now in `BDTest` |
| --- | --- |
| `BDTest.Test.Steps` and its `Given`, `When`, `Then` subnamespaces | All concrete step kinds, `StepBuilder`, the three continuation bases, `StepType` and its extensions |
| `BDTest.Attributes` | Story, scenario, step text, retry, tag, skip and test-information attributes |
| `BDTest.Test` | `BDTestBase`, `AbstractContextBDTestBase<T>`, `BDTestBuilder`, `BDTestScenario`, `BuildableTest`, `RunnableStepBuilder`, `ScenarioOutlineBuilder<T>` |
| `BDTest.Test` | `BDTestContext`, `ContextCreation`, context factories/lifetime interfaces, `ScenarioHookContext`, structured step arguments and timeout/argument extensions |
| `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit` | Adapter bases, composition hosts/interfaces, lifecycle attributes/hooks and assembly-run helpers |

For example, an NUnit test now needs one BDTest import alongside NUnit's own import:

```csharp sample="NUnit/NamespaceExample.cs"
using BDTest;
using NUnit.Framework;

public class Example : NUnitBDTestBase<object>
{
    [Test, ScenarioText("A simple scenario")]
    public async Task Runs()
    {
        await Given(() => { }).When(() => { }).Then(() => { });
    }
}
```

Concrete return types remain unchanged: `Given(...).And(...)` returns `AndGiven`,
and `Then(...).But(...)` returns `ButThen`. Custom adapters retain the protected
[lifecycle, startup-output, pending-execution and assertion-scope API](../frameworks/custom-adapters.md).
No additional friend assembly access is required.

Keep `using BDTest.Test;` where you explicitly name result types such as
`Scenario`, `Step`, `Status`, `ScenarioAttempt` or `ScenarioDefinitionSummary`.
These result models and the independent `BDTest.Contracts` DTOs retain their
namespaces. Report JSON property names, discriminators and historical payloads
are unchanged. Report renderer/storage namespaces are also unchanged.

Package IDs, assembly names, signing and target frameworks are unchanged. Install
matching versions of the core, adapter, source generator and analyzer packages.
Update reflection strings, fully qualified names and custom compiler tooling that
refer to the moved types; do not replace package references with namespace names.
