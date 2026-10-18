---
title: Skipping steps and scenarios
description: Skip individual steps with SkipStepAttribute rules, skip whole scenarios with global rules, and understand how skips are reported.
---

# Skipping steps and scenarios

## Skipping individual steps

Create an attribute that derives from `SkipStepAttribute`, put it on step methods, and register a rule that decides when those steps are skipped:

```csharp
using BDTest.Attributes;

public class SkipInProductionAttribute : SkipStepAttribute
{
}

public class SkipWhenAttribute : SkipStepAttribute
{
    public SkipWhenAttribute(string environment) => Environment = environment;
    public string Environment { get; }
}
```

```csharp
public class DataSteps
{
    [SkipInProduction]
    [StepText("I reset the test database")]
    public Task ResetTheDatabase() { ... }

    [SkipWhen("Staging")]
    public Task SeedLargeDataSet() { ... }
}
```

Register the rules once, during test-run setup:

```csharp
var environment = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");

BDTestSettings.GlobalSkipStepRules.Add<SkipInProductionAttribute>(_ => environment == "Production");
BDTestSettings.GlobalSkipStepRules.Add<SkipWhenAttribute>(attribute => attribute.Environment == environment);
```

A skipped step is not executed and is reported as `Skipped`. The rest of the scenario continues.

## Skipping whole scenarios

`GlobalSkipTestRules` skips a scenario before any of its steps run. The rule receives the `Scenario`, including its test class, story, tags and text:

```csharp
BDTestSettings.GlobalSkipTestRules.Add(scenario =>
    scenario.BdTestBaseClass is PaymentTests && !PaymentProviderIsAvailable());

BDTestSettings.GlobalSkipTestRules.Add(scenario =>
    scenario.Tags.Contains("manual"));
```

## Skipping from a step

Throw your framework's skip exception from a step, for example `Skip.Test("reason")` in TUnit, `Assert.Ignore()` in NUnit or `Assert.Skip("reason")` in xUnit. The adapters register these exceptions as **skip** exceptions; see [Exception classification](../configuration/exceptions.md).

A framework skip exception aborts the scenario without retries. It marks that step and the remaining unexecuted steps as `Skipped` and keeps previously passed steps as `Passed`. Genuine inconclusive exceptions still give `Inconclusive`.

## How skips are reported

| Situation | Scenario status |
| --- | --- |
| A global skip-test rule matches | `Skipped` |
| Every step is skipped | `Skipped` |
| Some steps are skipped and every executed step passes | `Passed` |
| A framework skip exception is thrown | `Skipped` |

The [`Debug.ShouldSkipWhenStep`](../configuration/settings.md#debugging-tests) setting also counts as intentional skipping: a scenario with passing `Given`/`Then` steps and skipped `When` steps is `Passed`.

### Test runner outcome

When a whole scenario is skipped, the adapters tell the test runner:

| Adapter | Runner outcome |
| --- | --- |
| NUnit | `Assert.Ignore(reason)`: the test is ignored. |
| TUnit | Throws `SkipTestException`: the test is skipped. |
| MSTest | `Assert.Inconclusive(reason)`: the runner shows a skipped or not-executed result. BDTest still records `Skipped`. An explicit `Assert.Inconclusive` inside a step stays `Inconclusive` in BDTest. |
| xUnit v3 | `Assert.Skip(reason)`: the test is skipped. |

Standalone `BDTestBase` and `BDTestBuilder` callers can inspect the returned skipped `Scenario`. [Custom adapters](../frameworks/custom-adapters.md) can override `OnScenarioSkipped(string reason)` to call their runner's skip mechanism.
