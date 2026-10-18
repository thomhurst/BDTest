---
title: MSTest
description: Use BDTest with MSTest through MSTestBDTestBase<TContext>.
---

# MSTest

```bash
dotnet add package BDTest.MSTest
```

`BDTest.MSTest` targets `netstandard2.0;net8.0` and depends on MSTest 4 (`MSTest.TestFramework`). Add `Microsoft.NET.Test.Sdk` and `MSTest.TestAdapter` to the test project.

## Writing tests

Inherit from `MSTestBDTestBase<TContext>` and mark the class with `[BDTestClass]`:

```csharp
using BDTest.Attributes;
using BDTest.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[BDTestClass]
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

Contexts may have constructor dependencies: override `CreateContextAsync(ContextCreation)` to construct them. BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps and disposes owned contexts at teardown, preferring `IAsyncDisposable` over `IDisposable`. Call `await InitializeContextAsync()` when async fixture setup needs an initialized context before any scenario. See [Test context](../writing-tests/test-context.md) for factories, retries and ownership.

## What the adapter does

- **Execution ID:** the fully qualified class name and the test's display name (which includes the data row), plus a unique suffix so repeated executions stay separate.
- **Initialisation:** a `[TestInitialize]` method registers `AssertInconclusiveException` as inconclusive and calls `BeginTest`.
- **Cleanup:** an asynchronous `[TestCleanup]` method awaits context disposal and completes the test.
- **Skips:** a whole-scenario skip calls `Assert.Inconclusive(reason)`. The runner shows it as skipped or not executed; BDTest records `Skipped`. An explicit `Assert.Inconclusive()` inside a step stays `Inconclusive` in BDTest.

The adapter exposes the runner-injected `TestContext` as `TestContext?`. It is absent before injection and can remain absent when a custom harness calls `InitializeBDTest()` directly. Check for `null` before accessing runner metadata. The adapter continues to support initialization without a runner context.

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

## Migrating method metadata

Replace `[TestClass]` with `[BDTestClass]` on each concrete class that derives from `MSTestBDTestBase<TContext>`. MSTest does not inherit class attributes from the adapter base. `BDTestClassAttribute` wraps the original `TestMethodAttribute.ExecuteAsync` and supplies `ITestMethod.MethodInfo` before initialization, including data rows and overloads. Custom test-method attributes still execute through their original implementation. A plain `[TestClass]` on a BDTest fixture now produces a migration error during initialization.

If you already derive your own class attribute from `TestClassAttribute`, delegate its `GetTestMethodAttribute` result to `new BDTestClassAttribute().GetTestMethodAttribute(original)` so both execution wrappers run.
