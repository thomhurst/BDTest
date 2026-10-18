---
title: xUnit
description: Use BDTest with xUnit v2 through XUnitBDTestBase<TContext>.
---

# xUnit

```bash
dotnet add package BDTest.xUnit
```

`BDTest.xUnit` targets `netstandard2.0` and supports **xUnit v2** (`xunit` 2.9). Add `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` to the test project.

## Writing tests

Inherit from `XUnitBDTestBase<TContext>` and pass xUnit's `ITestOutputHelper` to the base constructor:

```csharp
using BDTest.Attributes;
using BDTest.xUnit;
using Xunit;
using Xunit.Abstractions;

[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : XUnitBDTestBase<BasketContext>
{
    public BasketTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private BasketSteps Basket => new(Context);

    [Fact]
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

## What the adapter does

- **Execution ID:** the base constructor reads the current test case's `UniqueID` from the `ITestOutputHelper` instance.
- **Output:** BDTest captures console output itself. It does not write to `ITestOutputHelper`.
- **Teardown:** the class implements `IDisposable`. xUnit disposes the test class after each test, which removes the context and completes the test. If you override disposal, call the base `Dispose()`.
- **Exceptions:** none are registered. xUnit v2 assertions have no success, skip or inconclusive exceptions.

## Skipping

xUnit v2 has no runtime skip. If a whole scenario is skipped by a [global rule](../writing-tests/skipping.md#skipping-whole-scenarios), the adapter throws `NotSupportedException` so the runner cannot report a false pass. Skip at discovery time instead:

```csharp
[Fact(Skip = "Waiting for the payments sandbox")]
public async Task PaysByCard() { ... }
```

Skip-step rules still work, because the scenario itself keeps running.

## Reports after the run

xUnit v2 has no assembly-level teardown. Choose one of these:

- **Collection fixture.** Put the tests in one `[Collection]` whose fixture writes or uploads the report in `Dispose()`. Tests in one collection do not run in parallel with each other.
- **Process exit.** Write report files from an `AppDomain.CurrentDomain.ProcessExit` handler registered by a module initializer. Keep the work short and synchronous; the runner may stop the process soon after.

```csharp
using System.Runtime.CompilerServices;
using BDTest.Helpers;

internal static class BDTestReports
{
    [ModuleInitializer]
    internal static void Register() =>
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            File.WriteAllText("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport());
}
```
