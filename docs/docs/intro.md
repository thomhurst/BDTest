---
title: What is BDTest?
description: BDTest is a code-only behaviour-driven testing and reporting framework for .NET.
slug: /
sidebar_label: Introduction
---

# What is BDTest?

BDTest is a testing and reporting framework for .NET built around behaviour-driven testing. You write scenarios as `Given` / `When` / `Then` chains in plain C#. BDTest runs them inside your normal test runner and turns the results into reports that non-developers can read.

```csharp title="Illustrative fragment"
[Story(AsA = "account holder", IWant = "to withdraw money", SoThat = "I can use my savings")]
public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
    [Test]
    [ScenarioText("Withdrawing money reduces the account balance")]
    public async Task WithdrawMoney()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => AccountAssertions.TheBalanceIs(75m));
    }
}
```

The report for this test reads:

> **As an** account holder **I want** to withdraw money **so that** I can use my savings
>
> **Withdrawing money reduces the account balance** — Passed
> - Given I have an account with a balance of 100
> - When I withdraw 25
> - Then the account balance is 75

## Goals

BDTest structures tests so that they:

- are clear, concise and easy to understand.
- are easy to run and debug.
- do not share state. Each test is independent and free of side effects, so tests can run in parallel.
- describe business acceptance criteria: how an end user uses the system under test.
- produce results that you can share with your team or the business.

## Why not SpecFlow?

BDTest started as a reaction to SpecFlow. Think of BDTest as a pure code alternative:

- **No Gherkin files.** There is no second language to learn and keep in sync with the code.
- **No generated tests.** Your test bodies are your code and run as written, inside your usual runner. The bundled source generator only records step metadata for reports.
- **Easier debugging.** Stack traces point at your step methods, not at generated classes. You can step into every method from any IDE.
- **Richer arguments.** Pass enums, models, collections and other objects into steps.
- **Reusable steps.** A method is not tied to `Given`, `When` or `Then`. Use the same method in any position without extra attributes.

"Non-technical team members can write SpecFlow tests" is the usual counter-argument. In practice they can only combine steps that a developer already wrote. With BDTest, readable method names give them the same building blocks.

You still get business-readable output. Mark classes and methods with [`[Story]`, `[ScenarioText]`](writing-tests/stories-and-scenarios.md) and [`[StepText]`](writing-tests/step-text.md), or let BDTest convert method names into text.

## What is in the box

| Package | Purpose |
| --- | --- |
| `BDTest` | Core framework: builders, context, step text, settings and bundled compile-time step metadata. |
| `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit` | Base classes that connect BDTest to each test framework. |
| `BDTest.Razor.Reports` | Report server controllers and views for an ASP.NET Core app. |
| `BDTest.Reports.Storage.AzureBlob` | Persistent Azure Blob report storage. |

## Next steps

- [Install the packages](getting-started/installation.md)
- [Write your first test](getting-started/first-test.md)
- [Set up a report server](report-server/overview.md)
