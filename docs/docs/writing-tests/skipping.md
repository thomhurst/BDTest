---
title: Skipping steps and scenarios
description: Skip individual steps with SkipStepAttribute rules, skip whole scenarios with global rules, and understand how skips are reported.
---

# Skipping steps and scenarios

Configure `BDTestOptions` through [assembly configuration](../configuration/assembly-runs.md), or before starting an [explicit run](../advanced/explicit-runs.md). The examples below belong in that configuration.


## Skipping individual steps

Create an attribute that derives from `SkipStepAttribute`, put it on step methods, and register a rule that decides when those steps are skipped:

```csharp title="Illustrative fragment"
using BDTest;

public class SkipInProductionAttribute : SkipStepAttribute
{
}

public class SkipWhenAttribute : SkipStepAttribute
{
    public SkipWhenAttribute(string environment) => Environment = environment;
    public string Environment { get; }
}
```

```csharp title="Illustrative fragment"
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

```csharp title="Illustrative fragment"
var environment = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");

options.SkipStepRules.Add<SkipInProductionAttribute>(_ => environment == "Production");
options.SkipStepRules.Add<SkipWhenAttribute>(attribute => attribute.Environment == environment);
```

A skipped step is not executed and is reported as `Skipped`. The rest of the scenario continues.

Rules are evaluated once per reached step attempt, after all run and instance
before-step hooks. They see changes made by those hooks. Building or branching a
chain, refreshing its text, and using `WithStepText` do not evaluate skip rules.
A retry evaluates conditions again; a previous skip does not carry over. Unreached
steps and steps whose before hook fails do not evaluate conditions.

Evaluation uses one registration snapshot and stops at the first matching rule.
`BDTestRun.Start(options)` freezes registrations at startup; later additions to
`options.SkipStepRules` do not affect that run. Predicate closures can still read
changing state.

Skip attributes (including custom `SkipStepIf` attributes derived from
`SkipStepAttribute`) are resolved once per step definition, on first execution.
Treat attribute instances as immutable: definitions can be shared by branches and
concurrent executions. Method groups preserve attributes directly; simple lambda
calls need the [source generator](../advanced/source-generator.md).

A throwing predicate follows the normal step exception classification and scenario
retry policy. Step timing and output are recorded, and after-step hooks still run.
A later after-hook failure does not replace the predicate exception.

## Skipping whole scenarios

`SkipTestRules` skips a scenario before any of its steps run. The rule receives the `Scenario`, including its test class, story, tags and text:

```csharp title="Illustrative fragment"
options.SkipTestRules.Add(scenario =>
    scenario.BdTestBaseClass is PaymentTests && !PaymentProviderIsAvailable());

options.SkipTestRules.Add(scenario =>
    scenario.Tags.Contains("manual"));
```

Scenario predicates run once before the first attempt's asynchronous context initialization, scenario hooks and steps. For [outline rows](scenario-outlines.md), context construction/configuration and the row factory run first so the predicate receives the completed row definition. Matching skip rules still run scenario hooks and initialize their context. Evaluation stops at the first matching rule. Retries reuse this decision; changing state cannot cause a later attempt to become skipped through `SkipTestRules`. Step skip rules still evaluate on each reached step attempt.

A throwing scenario predicate fails the scenario without retrying or running hooks/steps, even if that exception type normally has a custom outcome. The original predicate exception is reported and thrown, and scenario output includes a `Scenario skip predicate failed` diagnostic. A started scenario can still be skipped by a framework skip exception.

## Skipping from a step

Throw your framework's skip exception from a step, for example `Skip.Test("reason")` in TUnit, `Assert.Ignore()` in NUnit or `Assert.Skip("reason")` in xUnit. The adapters register these exceptions as **skip** exceptions; see [Exception classification](../configuration/exceptions.md).

A framework skip exception aborts the scenario without retries. It marks that step and the remaining unexecuted steps as `Skipped` and keeps previously passed steps as `Passed`. Genuine inconclusive exceptions still give `Inconclusive`.

## How skips are reported

| Situation | Scenario status |
| --- | --- |
| A run skip-test rule matches | `Skipped` |
| Every step is skipped | `Skipped` |
| Some steps are skipped and every executed step passes | `Passed` |
| A framework skip exception is thrown | `Skipped` |

The [`Debug.ShouldSkipWhenStep`](../configuration/settings.md#debugging-tests) setting also counts as intentional skipping: a scenario with passing `Given`/`Then` steps and skipped `When` steps is `Passed`.

Each intentional step skip records a structured `Step.SkipReason`, also shown in
static HTML and hosted reports. For a rule it contains `Source = "Rule"`, the
attribute's full type name in `AttributeType`, and the zero-based `RuleIndex` in
that attempt's registration snapshot. For the debug setting it contains
`Source = "DebugSetting"` and `Setting = "Debug.ShouldSkipWhenStep"`. Attribute
rules are checked first; the debug setting applies if none match. It covers
`When`, `And` after `When`, and `But` after `When`.

The reason is cleared for each attempt. Historical reports without this optional
field, unreached steps, scenario-level skips and framework skip exceptions have no
step condition reason. A reason can remain on a failed step when an after-hook
fails after the step was intentionally skipped.

### Test runner outcome

When a whole scenario is skipped, the adapters tell the test runner:

| Adapter | Runner outcome |
| --- | --- |
| NUnit | `Assert.Ignore(reason)`: the test is ignored. |
| TUnit | Throws `SkipTestException`: the test is skipped. |
| MSTest | `Assert.Inconclusive(reason)`: the runner shows a skipped or not-executed result. BDTest still records `Skipped`. An explicit `Assert.Inconclusive` inside a step stays `Inconclusive` in BDTest. |
| xUnit v3 | `Assert.Skip(reason)`: the test is skipped. |

Standalone `BDTestBase` and `BDTestBuilder` callers can inspect the returned skipped `Scenario`. [Custom adapters](../frameworks/custom-adapters.md) can override `OnScenarioSkipped(string reason)` to call their runner's skip mechanism.
