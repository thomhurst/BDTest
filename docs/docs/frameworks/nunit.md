---
title: NUnit
description: Use BDTest with NUnit through NUnitBDTestBase<TContext>.
---

# NUnit

```bash
dotnet add package BDTest.NUnit
```

`BDTest.NUnit` targets `net8.0` and depends on NUnit 5. Add `Microsoft.NET.Test.Sdk` and `NUnit3TestAdapter` to the test project as usual.

## Writing tests

Inherit from `NUnitBDTestBase<TContext>`:

```csharp
using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : NUnitBDTestBase<BasketContext>
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

`[TestFixture]` is already on the base class, and you can run fixtures and tests in parallel with `[Parallelizable]`.

Contexts may have constructor dependencies: override `CreateContextAsync(ContextCreation)` to construct them. BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps and disposes owned contexts at teardown, preferring `IAsyncDisposable` over `IDisposable`. Call `await InitializeContextAsync()` when async fixture setup needs an initialized context before any scenario. See [Test context](../writing-tests/test-context.md) for factories, retries and ownership.

## What the adapter does

- **Execution ID:** `TestContext.CurrentContext.Test.ID` plus the current repeat count, so `[Repeat]` runs stay separate.
- **Exceptions:** registers `SuccessException` (from `Assert.Pass`) as success, `IgnoreException` (from `Assert.Ignore`) as skip and `InconclusiveException` as inconclusive. Registration happens once, in a `[OneTimeSetUp]`.
- **Setup:** a `[SetUp]` method calls `BeginTest` with the test ID and repeat count, which ties context and console output to the current test.
- **Teardown:** an asynchronous `[TearDown]` method awaits context disposal and completes the test. Existing `MarkTestAsComplete()` overrides remain supported through the synchronous compatibility path.
- **Retries:** each attempt runs its steps and step hooks through NUnit's public exception constraint API. A failed attempt does not change the NUnit result when a later attempt passes. Failures outside the scenario remain failures, and scenario warnings remain visible. When the final attempt throws after recording other assertion failures, NUnit reports the exception together with those assertion diagnostics. Retry rules still receive the original exception. The adapter never overwrites the NUnit result during teardown.
- **Skips:** a whole-scenario skip calls `Assert.Ignore(reason)`.

You can add your own `[SetUp]` and `[TearDown]` methods. They run alongside the adapter's.

## Reports after the run

Use an NUnit `[SetUpFixture]` in the root namespace of the test assembly (or with no namespace) to run code after all tests:

```csharp
using BDTest;
using BDTest.Helpers;
using NUnit.Framework;

[SetUpFixture]
public class BDTestReports
{
    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        var descriptor = new BDTestRunDescriptor { Environment = "CI", Tag = "Acceptance" };

        File.WriteAllText("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport(descriptor));

        // Or upload to a report server:
        // var uri = await BDTestReportServer.SendDataAndGetReportUriAsync(serverUri, descriptor, httpClient);
    }
}
```

See [HTML report](../reports/html-report.md) and [Sending results](../report-server/sending-results.md).

The adapter supplies the runner's exact test method to `BeginTest(id, methodInfo)`. Scenario text, information, tags and retry attributes use cached method metadata, including when a helper constructs the scenario after an await. See [custom adapter migration](custom-adapters.md#method-identity-and-migration).
