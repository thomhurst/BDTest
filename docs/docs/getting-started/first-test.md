---
title: Your first test
description: Write, run and report a complete BDTest scenario with TUnit.
---

# Your first test

This walkthrough builds a complete scenario with [TUnit](https://tunit.dev). It uses an in-memory account, so it needs no server or credentials.

Using another framework? The code is the same apart from the base class, the test attributes and the assertions. See [Test frameworks](../frameworks/overview.md).

## 0. Install the packages

```bash
dotnet add package TUnit
dotnet add package BDTest.TUnit
dotnet add package BDTest.SourceGenerator
```

`TUnit` brings the test framework, assertions and runner; `BDTest.TUnit` only depends on `TUnit.Core`. The source generator is what turns `Account.CreateAnAccount(100m)` into "I have an account with a balance of 100" in the report. See [Installation](installation.md) for a full project file.

## 1. Create a context

A context holds the state for one test. BDTest creates a new instance for each test, so tests never share state.

```csharp title="AccountContext.cs"
public class AccountContext
{
    public decimal Balance { get; set; }
}
```

The context class needs a public parameterless constructor. See [Test context](../writing-tests/test-context.md).

## 2. Write step classes

Steps are ordinary methods. Group them into classes that receive the context in their constructor. `[StepText]` controls how each step appears in the report; `{0}` is replaced by the first argument.

```csharp title="AccountSteps.cs"
using BDTest.Attributes;

public class AccountSteps
{
    private readonly AccountContext _context;

    public AccountSteps(AccountContext context) => _context = context;

    [StepText("I have an account with a balance of {0}")]
    public void CreateAnAccount(decimal openingBalance) => _context.Balance = openingBalance;

    [StepText("I withdraw {0}")]
    public void Withdraw(decimal amount) => _context.Balance -= amount;
}

public class AccountAssertionsSteps
{
    private readonly AccountContext _context;

    public AccountAssertionsSteps(AccountContext context) => _context = context;

    [StepText("the account balance is {0}")]
    public async Task TheBalanceIs(decimal expectedBalance) =>
        await Assert.That(_context.Balance).IsEqualTo(expectedBalance);
}
```

TUnit assertions are awaited, so the assertion step returns `Task`. BDTest accepts synchronous and asynchronous steps in any position.

## 3. Write the scenario

Inherit from `TUnitBDTestBase<TContext>`. Expose step classes as **properties**, not fields, so that each test gets steps bound to its own `Context`.

```csharp title="WithdrawalTests.cs"
using BDTest.Attributes;
using BDTest.TUnit;

[Story(AsA = "account holder",
    IWant = "to withdraw money",
    SoThat = "I can use my savings")]
public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
    private AccountSteps Account => new AccountSteps(Context);
    private AccountAssertionsSteps AccountAssertions => new AccountAssertionsSteps(Context);

    [Test]
    [ScenarioText("Withdrawing money reduces the account balance")]
    public async Task WithdrawMoney()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => AccountAssertions.TheBalanceIs(75m))
            .BDTestAsync();
    }
}
```

Test methods return `Task` and await the chain. You can also await the chain directly without calling `BDTestAsync()`.

## 4. Run it

TUnit test projects are executables that run on Microsoft.Testing.Platform:

```bash
dotnet run --project MyApp.AcceptanceTests
```

BDTest records the scenario, its steps, their status and any console output. If a step throws, the exception is recorded against that step, the remaining steps are marked **Not run**, and the exception is rethrown to the test runner.

## 5. Produce a report

Add an assembly-level hook that writes an HTML report after all tests have run:

```csharp title="BDTestReports.cs"
using BDTest;
using BDTest.Helpers;

public class BDTestReports
{
    [After(HookType.Assembly)]
    public static async Task WriteReport()
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor
        {
            Environment = "Local",
            Tag = "Acceptance tests",
        });

        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "BDTest-Report.html"), html);
    }
}
```

Open `BDTest-Report.html` from the test project's output folder in a browser. To share results with a team, use the [report server](../report-server/overview.md) instead.

## Next steps

- [Scenario rules](../writing-tests/scenarios.md): which steps can follow which.
- [Step text](../writing-tests/step-text.md): control how steps read in reports.
- [Scenario outlines](../writing-tests/scenario-outlines.md): run one scenario over a table of examples.
