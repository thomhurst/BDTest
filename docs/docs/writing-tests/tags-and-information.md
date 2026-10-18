---
title: Tags and test information
description: Tag scenarios for filtering with [BDTestTag] and attach extra details with [TestInformation].
---

# Tags and test information

## Tags

Use `[BDTestTag]` from `BDTest.Attributes` on a scenario method or a story class. Apply it several times for several tags.

```csharp
[BDTestTag("checkout")]
public class CheckoutTests : TUnitBDTestBase<CheckoutContext>
{
    [Test]
    [BDTestTag("smoke")]
    [BDTestTag("payments")]
    public async Task SuccessfulCheckout()
    {
        await Given(() => Basket.HasItems())
            .When(() => Checkout.Pay())
            .Then(() => Orders.OrderIsPlaced())
            .BDTestAsync();
    }
}
```

- Class tags apply to every scenario in the class, including tags inherited from base classes.
- Method tags combine with class tags. Duplicates are removed, ignoring case.
- Tag names are trimmed and cannot be empty.

Tags are available on `BuildableTest.Tags` and `Scenario.Tags` and are written to JSON output, including for tests that did not run.

In the report server's **Scenarios** and **Stories** pages, pick a tag and press **Apply** to filter scenarios before grouping and paging. Tag matching ignores case and combines with the status filter. Choose **All tags** to clear the filter.

Scenario tags are separate from the run-level `BDTestRunDescriptor.Tag`, which labels a whole test run. See [Sending results](../report-server/sending-results.md).

### Using tags in rules

Tags are on the `Scenario`, so a [global skip rule](skipping.md#skipping-whole-scenarios) can use them:

```csharp
BDTestSettings.GlobalSkipTestRules.Add(scenario =>
    scenario.Tags.Contains("slow") && Environment.GetEnvironmentVariable("RUN_SLOW_TESTS") != "true");
```

## Test information

`[TestInformation]` attaches extra text to a scenario in the report, for example a bug reference or a link to a requirement. You can apply it several times.

```csharp
[Test]
[TestInformation("Regression test for bug #12345")]
public async Task Test1()
{
    ...
}
```

### Custom information attributes

Subclass `TestInformationAttribute` to build reusable attributes:

```csharp
public class BugInformationAttribute : TestInformationAttribute
{
    public BugInformationAttribute(string bugNumber)
        : base($"Bug #{bugNumber} - https://bugs.example.com/{bugNumber}")
    {
    }
}

[Test]
[BugInformation("12345")]
public async Task Test1()
{
    ...
}
```

The report shows each entry as `AttributeTypeName - Information`. The values are also available on `Scenario.CustomTestInformation`.
