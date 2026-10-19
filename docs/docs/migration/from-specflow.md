---
title: Coming from SpecFlow
description: Map SpecFlow and Gherkin concepts to BDTest code.
---

# Coming from SpecFlow

SpecFlow (and Reqnroll) bind Gherkin text to C# methods with regular expressions and generate test classes from `.feature` files. BDTest skips the text layer: the C# chain **is** the scenario, and the report text comes from attributes or method names, read at compile time by the source generator bundled in `BDTest`.

## Concept map

| SpecFlow / Gherkin | BDTest |
| --- | --- |
| `.feature` file | A test class |
| `Feature:` with "As a / I want / So that" | `[Story(AsA = ..., IWant = ..., SoThat = ...)]` on the class |
| `Scenario:` | A test method with `[ScenarioText("...")]` |
| `Given` / `When` / `Then` / `And` / `But` lines | `Given(...)`, `When(...)`, `Then(...)`, `.And(...)`, `.But(...)` |
| `[Binding]` step definitions with regex | Ordinary methods, optionally with `[StepText("... {parameterName} ...")]` |
| Step argument transformations | Typed arguments, plus [`IStepTextStringConverter<T>`](../writing-tests/step-text.md#custom-converters) for display |
| `ScenarioContext` / context injection | The per-test [`Context`](../writing-tests/test-context.md) object |
| `Scenario Outline:` + `Examples:` | [`ScenarioOutline(...)`](../writing-tests/scenario-outlines.md) with typed rows |
| `@tags` | [`[BDTestTag("...")]`](../writing-tests/tags-and-information.md) |
| `[BeforeStep]` / `[AfterStep]` hooks | [`OnBeforeStep` / `OnAfterStep`](../writing-tests/step-hooks.md) or `IStepHook` registrations on `BDTestOptions.StepHooks` |
| `[BeforeScenario]` / `[AfterScenario]` | Your framework's per-test hooks, such as TUnit's `[Before(HookType.Test)]` / `[After(HookType.Test)]` |
| `@ignore` | Your framework's ignore attribute, or a [skip rule](../writing-tests/skipping.md) |
| `ScenarioContext.Pending()` | Throw `NotImplementedException`: the step becomes `NotImplemented` |
| LivingDoc / reports | The [HTML report](../reports/html-report.md) or [report server](../report-server/overview.md) |

## Example

```gherkin
Feature: Withdrawals
  As an account holder
  I want to withdraw money
  So that I can use my savings

  Scenario: Withdrawing money reduces the balance
    Given I have an account with a balance of 100
    When I withdraw 25
    Then the account balance is 75
```

becomes:

```csharp title="Illustrative fragment"
[Story(AsAn = "account holder", IWant = "to withdraw money", SoThat = "I can use my savings")]
public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
    private AccountSteps Account => new(Context);

    [Test]
    [ScenarioText("Withdrawing money reduces the balance")]
    public async Task WithdrawingMoneyReducesTheBalance()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => Account.TheBalanceIs(75m));
    }
}
```

The step definitions become methods with `[StepText]`:

```csharp title="Illustrative fragment"
public class AccountSteps(AccountContext context)
{
    [StepText("I have an account with a balance of {balance}")]
    public void CreateAnAccount(decimal balance) => context.Balance = balance;

    [StepText("I withdraw {amount}")]
    public void Withdraw(decimal amount) => context.Balance -= amount;

    [StepText("the account balance is {expected}")]
    public async Task TheBalanceIs(decimal expected) => await Assert.That(context.Balance).IsEqualTo(expected);
}
```

## Migration tips

- Migrate one feature at a time. BDTest and SpecFlow tests can live in the same solution, in separate projects.
- Turn each regex step definition into a method with a readable name. Often the method name alone produces good step text, so `[StepText]` is optional.
- Move `ScenarioContext` keys into properties on a typed context class.
- Replace table arguments with typed objects or collections.
- Use `.When(...).And(...).Then(...)` for scenarios with multiple actions. `And` and `But` keep the current phase.
