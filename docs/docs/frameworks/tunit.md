---
title: TUnit
description: Use BDTest with TUnit through composition or TUnitBDTestBase<TContext>.
---

# TUnit

The adapter uses a [configured assembly run](../configuration/assembly-runs.md). Configure `BDTestOptions` with `[assembly: BDTestConfiguration(typeof(TestConfiguration))]`. Use `TUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly)` for reports and call `TUnitBDTestAssembly.Dispose(assembly)` after all tests and exports finish. Explicit-run overrides remain supported.


TUnit is the recommended framework for BDTest, and the one used in the examples throughout this documentation.

```bash
dotnet add package BDTest.TUnit
```

`BDTest.TUnit` targets `netstandard2.0;net8.0` and depends on `TUnit.Core`. Reference the `TUnit` package in the test project.

## Composition with an existing base class

Implement `IBDTestTUnitTest` and return a stable host created for the actual test instance. No attribute, manual lifecycle calls, or separate hook registration is needed. The adapter registers global TUnit hooks for both composition and inheritance.

```csharp sample="TUnit/Composition.cs" title="Compiled example"
using BDTest;
using BDTest.Settings;
using TUnit.Core;

public class ComposedBasketTests : CompanyTestBase, IBDTestTUnitTest
{
    internal readonly BDTestRun Run;
    private readonly BDTestScenario<BasketContext> _bdt;

    public ComposedBasketTests()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(TUnitBDTestLifecycle.OutcomeMap);
        Run = BDTestRun.Start(options);
        _bdt = BDTestScenario.For<BasketContext>(this, Run);
    }

    public BDTestBase BDTestHost => _bdt;

    [Before(HookType.Test)]
    public async Task PrepareBasket()
    {
        await _bdt.InitializeContextAsync();
        Console.WriteLine("Preparing basket");
    }

    [Test]
    public async Task AddingAnItem()
    {
        await _bdt.Given(() => _bdt.Context.Empty())
            .When(() => _bdt.Context.Add("Book", 12.50m))
            .Then(() => _bdt.Context.VerifyTotal(12.50m))
            .BDTestAsync();
    }
}

public class ComposedBasketReports
{
    // Runs after the adapter's global completion hook. This example owns one run
    // per test instance; write its report and dispose it only after completion.
    [AfterEvery(HookType.Test, Order = int.MaxValue)]
    public static async Task SaveReport(TestContext context)
    {
        if (context.Metadata.TestDetails.ClassInstance is ComposedBasketTests test)
        {
            try
            {
                await File.WriteAllTextAsync($"{test.Run.ReportId}.html",
                    BDTest.Helpers.BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), test.Run));
            }
            finally { test.Run.Dispose(); }
        }
    }
}
```

Choose the owning [explicit run](../advanced/explicit-runs.md) when creating the host. Register `TUnitBDTestLifecycle.OutcomeMap` before starting the run so framework exceptions and whole-scenario skips reach TUnit. The adapter never disposes your run. A run can instead be shared by an assembly or suite; its owner must wait for every test's completion before reporting and disposing it. Alternatively, `TUnitBDTestScenario.For<TContext>(this)` uses the configured assembly run and its outcome map; retain `IBDTestTUnitTest` registration and dispose through `TUnitBDTestAssembly` after all tests finish.

TUnit supplies the actual test method and its unique execution ID, including data rows and repeats. Story, scenario text, tags, and retry attributes come from the owning instance and method. Read `_bdt.Context` inside steps so retries see the replacement context. Configure step and scenario callbacks on `BDTestOptions` when using composition; virtual instance hooks remain available on the inheritance API.

## Writing tests with inheritance

Inherit from `TUnitBDTestBase<TContext>`:

```csharp sample="TUnit/Inheritance.cs" title="Compiled example"
using BDTest;

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

Contexts may have constructor dependencies: override `CreateContextAsync(ContextCreation)` to construct them. BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps and disposes owned contexts at teardown, preferring `IAsyncDisposable` over `IDisposable`. Call `await InitializeContextAsync()` when async fixture setup needs an initialized context before any scenario. See [Test context](../writing-tests/test-context.md) for factories, retries and ownership.

## What the adapter does

- **Execution ID:** `TestContext.Current.Id`.
- **Exceptions:** registers `SkipTestException` (from `Skip.Test(...)`) as skip and `InconclusiveTestException` as inconclusive, on the frozen assembly run before tests start.
- **Opt-in:** `IBDTestTUnitTest.BDTestHost` selects the host and its run. `TUnitBDTestBase<TContext>` implements this interface automatically.
- **Setup:** a global `[BeforeEvery(HookType.Test, Order = int.MinValue)]` hook calls `BeginTest` and exports BDTest's `AsyncLocal` values before instance setup.
- **Teardown:** a global `[AfterEvery(HookType.Test, Order = int.MaxValue - 1)]` hook awaits context disposal and completes the same host after instance teardown, including setup/body/teardown failures. Completion also clears output when no scenario runs.
- **Skips:** a whole-scenario skip throws `SkipTestException`, so TUnit reports the test as skipped.

These hooks work with both package targets (`netstandard2.0;net8.0`); they do not depend on event-receiver stages that are only available on newer TUnit targets. Reserve `Order = int.MaxValue` for checks and report/disposal hooks that must run after BDTest completion. Ordinary setup and teardown hooks can keep their default order.

To migrate from manual composition, implement `IBDTestTUnitTest`, return your existing host, and remove manual `BeginTest`/`EndTest` calls. To migrate from `TUnitBDTestBase<TContext>`, create a composition host with the same run, return it through the interface, and qualify builder/context calls with that host. Existing `Setup`, `TearDown`, `BDTestTUnitSetup`, and `BDTestTUnitTearDown` methods remain callable for custom harnesses; native TUnit execution uses the shared global lifecycle instead.

## Reports after the run

Use an assembly-level `[After]` hook:

```csharp sample="TUnit/AssemblyReport.cs" title="Compiled example"
using BDTest.Helpers;

public class BDTestReports
{
    [After(HookType.Assembly)]
    public static async Task AfterAllTests()
    {
        var assembly = typeof(BDTestReports).Assembly;
        try
        {
            var run = BDTest.TUnitBDTestAssembly.GetRun(assembly);
            await File.WriteAllTextAsync("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport(null, run));
        }
        finally { BDTest.TUnitBDTestAssembly.Dispose(assembly); }
    }
}
```

TUnit 1.75.0 reports a propagated `InconclusiveTestException` as Failed in TRX,
while BDTest records Inconclusive. This difference is covered by the
[adapter conformance suite](../configuration/exceptions.md#runner-differences).

The adapter supplies the runner's exact test method to `BeginTest(id, methodInfo)`. Scenario text, information, tags and retry attributes use cached method metadata, including when a helper constructs the scenario after an await. See [custom adapter migration](custom-adapters.md#method-identity-and-migration).
