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

## What the adapter does

- **Execution ID:** `TestContext.CurrentContext.Test.ID` plus the current repeat count, so `[Repeat]` runs stay separate.
- **Exceptions:** registers `SuccessException` (from `Assert.Pass`) as success, `IgnoreException` (from `Assert.Ignore`) as skip and `InconclusiveException` as inconclusive. Registration happens once, in a `[OneTimeSetUp]`.
- **Setup:** a `[SetUp]` method ties console output to the current test.
- **Teardown:** a `[TearDown]` method removes the test's context and completes the test. If a [retried](../writing-tests/retries.md) scenario finally passed, it also sets the NUnit result to success.
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
