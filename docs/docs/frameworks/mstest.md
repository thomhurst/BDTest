---
title: MSTest
description: Use BDTest with MSTest through MSTestBDTestBase<TContext>.
---

# MSTest

```bash
dotnet add package BDTest.MSTest
```

`BDTest.MSTest` targets `netstandard2.0` and depends on MSTest 4 (`MSTest.TestFramework`). Add `Microsoft.NET.Test.Sdk` and `MSTest.TestAdapter` to the test project.

## Writing tests

Inherit from `MSTestBDTestBase<TContext>` and mark the class with `[TestClass]`:

```csharp
using BDTest.Attributes;
using BDTest.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : MSTestBDTestBase<BasketContext>
{
    private BasketSteps Basket => new(Context);

    [TestMethod]
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

- **Execution ID:** a new GUID per fixture instance. MSTest creates an instance per test, including each data-driven case, so async setup, context access and cleanup stay together.
- **Initialisation:** a `[TestInitialize]` method registers `AssertInconclusiveException` as inconclusive and marks the test as started.
- **Cleanup:** a `[TestCleanup]` method removes the context and completes the test.
- **Skips:** a whole-scenario skip calls `Assert.Inconclusive(reason)`. The runner shows it as skipped or not executed; BDTest records `Skipped`. An explicit `Assert.Inconclusive()` inside a step stays `Inconclusive` in BDTest.

## Reports after the run

Use `[AssemblyCleanup]` on a static method in a `[TestClass]`:

```csharp
[TestClass]
public class BDTestReports
{
    [AssemblyCleanup]
    public static async Task AfterAllTests()
    {
        File.WriteAllText("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport());
        // or: await BDTestReportServer.SendDataAndGetReportUriAsync(...)
    }
}
```
