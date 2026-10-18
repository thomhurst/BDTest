---
title: TUnit
description: Use BDTest with TUnit through TUnitBDTestBase<TContext>.
---

# TUnit

TUnit is the recommended framework for BDTest, and the one used in the examples throughout this documentation.

```bash
dotnet add package BDTest.TUnit
```

`BDTest.TUnit` targets `netstandard2.0` and depends on `TUnit.Core`. Reference the `TUnit` package in the test project.

## Writing tests

Inherit from `TUnitBDTestBase<TContext>`:

```csharp
using BDTest.Attributes;
using BDTest.TUnit;

[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : TUnitBDTestBase<BasketContext>
{
    private BasketSteps Basket => new(Context);

    [Test]
    [ScenarioText("Adding an item updates the total")]
    public async Task AddingAnItem()
    {
        await Given(() => Basket.IsEmpty())
            .When(() => Basket.Add("Book", 12.50m))
            .Then(() => Basket.TheTotalIs(12.50m))
            .BDTestAsync();
    }
}
```

TUnit runs tests in parallel by default. Each test gets its own `Context`.

## Running

TUnit uses Microsoft.Testing.Platform. Run the test project directly:

```bash
dotnet run --project MyApp.AcceptanceTests
dotnet run --project MyApp.AcceptanceTests -- --treenode-filter "/*/*/BasketTests/*"
```

## What the adapter does

- **Execution ID:** `TestContext.Current.Id`.
- **Exceptions:** registers `SkipTestException` (from `Skip.Test(...)`) as skip and `InconclusiveTestException` as inconclusive, in a class-level `[Before]` hook.
- **Setup:** a `[Before(HookType.Test)]` hook marks the test as started and exports BDTest's `AsyncLocal` values so the test body sees them.
- **Teardown:** an `[After(HookType.Test)]` hook removes the context and completes the test.
- **Skips:** a whole-scenario skip throws `SkipTestException`, so TUnit reports the test as skipped.

## Reports after the run

Use an assembly-level `[After]` hook:

```csharp
using BDTest.Helpers;

public class BDTestReports
{
    [After(HookType.Assembly)]
    public static async Task AfterAllTests()
    {
        await File.WriteAllTextAsync("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport());
    }
}
```
