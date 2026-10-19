---
title: MSTest
description: Use BDTest with MSTest through composition or MSTestBDTestBase<TContext>.
---

# MSTest

The adapter uses a [configured assembly run](../configuration/assembly-runs.md). Configure `BDTestOptions` with `[assembly: BDTestConfiguration(typeof(TestConfiguration))]`. Use `MSTestBDTestAssembly.GetRun(typeof(MyTests).Assembly)` for reports and call `MSTestBDTestAssembly.Dispose(assembly)` after all tests and exports finish. Explicit-run overrides remain supported.


```bash
dotnet add package BDTest.MSTest
```

`BDTest.MSTest` targets `netstandard2.0;net8.0` and depends on MSTest 4 (`MSTest.TestFramework`). Add `Microsoft.NET.Test.Sdk` and `MSTest.TestAdapter` to the test project.

## Composition with an unrelated base class

Use `[BDTestClass]`, implement `IBDTestMSTestTest`, and forward MSTest's public `TestContext` setter to `MSTestBDTestLifecycle.Initialize`. The property needs both a public getter and setter for MSTest discovery. Return the same host throughout each test invocation.

```csharp sample="MSTest/Composition.cs" title="Compiled example"
using BDTest;
using BDTest.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[BDTestClass]
public class ComposedBasketTests : CompanyTestBase, IBDTestMSTestTest
{
    private readonly BDTestScenario<ComposedBasketContext> _bdd;
    private TestContext _testContext = null!;

    public ComposedBasketTests() => _bdd = MSTestBDTestScenario.For<ComposedBasketContext>(this, TestRuns.Baskets);

    public BDTestBase BDTestHost => _bdd;

    public TestContext TestContext
    {
        get => _testContext;
        set
        {
            _testContext = value;
            MSTestBDTestLifecycle.Initialize(this, value);
        }
    }

    [TestInitialize]
    public async Task Setup()
    {
        await _bdd.InitializeContextAsync();
        Console.WriteLine("Basket setup");
    }

    [TestMethod]
    public async Task AddingAnItem()
    {
        await _bdd.Given(() => _bdd.Context.Items.Clear())
            .When(() => _bdd.Context.Items.Add("Book"))
            .Then(() => Assert.AreEqual(1, _bdd.Context.Items.Count))
            .BDTestAsync();
    }
}

public sealed class ComposedBasketContext
{
    public List<string> Items { get; } = new();
}

internal static class TestRuns
{
    internal static BDTestRun Baskets { get; } = Create();

    private static BDTestRun Create()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(MSTestBDTestLifecycle.OutcomeMap);
        return BDTestRun.Start(options);
    }
}
```

The host selects its owning run when constructed. Register the MSTest outcome map **before** `BDTestRun.Start`, because explicit runs freeze their options. Each run owns its contexts, captured output, scenarios, and report. Hosts do not dispose runs. Create shared runs before their tests, export reports during assembly cleanup, then dispose runs after all their tests have completed. Multiple runs can execute in parallel, including identical framework IDs, without sharing reports or settings. Alternatively, `MSTestBDTestScenario.For<TContext>(this)` selects the configured assembly run, including its framework outcomes. Keep the same lifecycle registration and context injection.

MSTest injects `TestContext` before instance `[TestInitialize]` methods. The setter starts BDTest synchronously, so async setup and the body share context and console capture. `[BDTestClass]` wraps each native `ITestMethod.InvokeAsync` call and awaits `EndTestAsync` after MSTest finishes instance setup, body, cleanup, and disposal. Completion also runs after setup/body/cleanup failures, including tests that never construct a scenario. Context disposal failures reach the runner. Do not dispose a run in per-test cleanup while its host is still active.

Method metadata, class story/tags, and attributed retry hooks come from the owning fixture, including private retry hooks. Scenario retries recreate and dispose contexts through the same core lifecycle as inheritance. Data-row display names and a unique invocation suffix distinguish rows and custom test-method repeats. Custom `TestMethodAttribute` implementations retain control and must invoke the supplied `ITestMethod`, rather than invoking reflection methods directly. Constructor output and global initialization before context injection are outside this per-test capture boundary.

## Optional base class

Inherit from `MSTestBDTestBase<TContext>` and mark the class with `[BDTestClass]`:

```csharp sample="MSTest/Inheritance.cs" title="Compiled example"
using BDTest;
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
- **Initialisation:** native `TestContext` injection starts the selected host before instance setup. Both composition and inheritance use `MSTestBDTestLifecycle`.
- **Cleanup:** `[BDTestClass]` completes each native invocation and awaits context disposal after MSTest cleanup. The base class does not start or end a second execution.
- **Skips:** a whole-scenario skip calls `Assert.Inconclusive(reason)`. The runner shows it as skipped or not executed; BDTest records `Skipped`. An explicit `Assert.Inconclusive()` inside a step stays `Inconclusive` in BDTest.

The adapter exposes the runner-injected `TestContext` as `TestContext?`. It is absent before injection and can remain absent when a custom harness calls `InitializeBDTest()` directly. Check for `null` before accessing runner metadata. The adapter continues to support initialization without a runner context.

## Reports after the run

Add `BDTest.Reporting` at the same version as the adapter before using these export APIs.
See [reporting package migration](../migration/reporting-package.md).

Use `[AssemblyCleanup]` on a static method in a `[TestClass]`:

```csharp sample="MSTest/AssemblyReport.cs" title="Compiled example"
using BDTest;
using BDTest.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class BDTestReports
{
    [AssemblyCleanup]
    public static async Task AfterAllTests()
    {
        var assembly = typeof(BDTestReports).Assembly;
        try
        {
            var run = BDTest.MSTestBDTestAssembly.GetRun(assembly);
            await File.WriteAllTextAsync("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport(null, run));
        }
        finally { BDTest.MSTestBDTestAssembly.Dispose(assembly); }
        // or: await uploader.UploadReportAsync(run, descriptor)
    }
}
```

For the explicit run above, export and dispose that run in `[AssemblyCleanup]`:

```csharp sample="MSTest/ExplicitReport.cs" title="Compiled example"
using BDTest;
using BDTest.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class BasketReports
{
    [AssemblyCleanup]
    public static void AfterAllTests()
    {
        try
        {
            File.WriteAllText("Baskets.html",
                BDTest.Helpers.BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), TestRuns.Baskets));
        }
        finally { TestRuns.Baskets.Dispose(); }
    }
}
```

Context disposal runs in the owning BDTest run and execution, including when MSTest completes in a different execution context. Console messages written during asynchronous disposal appear in the scenario teardown output and exported report, even when disposal fails.

## Migrating method metadata

Replace `[TestClass]` with `[BDTestClass]` on each concrete class that uses BDTest. MSTest does not inherit class attributes from the adapter base. `BDTestClassAttribute` preserves the original `TestMethodAttribute.ExecuteAsync` and supplies `ITestMethod.MethodInfo` before initialization, including data rows and overloads. A plain `[TestClass]` on a BDTest fixture produces a migration error during context injection.

Existing inherited tests keep their builders and `Context`. To migrate to composition, keep `[BDTestClass]`, select an explicit run, create one `MSTestBDTestScenario.For<TContext>(this, run)` host, implement the interface and context setter above, and call builders/context on that host. Do not call `BeginTest` or `EndTest` yourself with this integration. The base class's `InitializeBDTest`, `BDTestMSTestTearDown`, and synchronous `TearDown` methods remain compatibility entry points for manual callers; they are no longer MSTest setup/cleanup hooks.

If you already derive your own class attribute from `TestClassAttribute`, delegate its `GetTestMethodAttribute` result to `new BDTestClassAttribute().GetTestMethodAttribute(original)` so both execution wrappers run.

## Cancellation

The inherited base and the framework-specific composition factory automatically forward
`TestContext.CancellationToken` (the current runner context for composition) to scenario and outline execution, token-aware steps, and hooks, even when no
BDTest timeout is configured. This API is verified against MSTest 4.5.1. Use
`[Timeout(..., CooperativeCancellation = true)]` or runner cancellation to signal the framework token.

`BDTestAsync(callerToken)` combines your token with the framework token; passing a caller
token does not disable runner cancellation. Either source stops execution before unreached
steps or retries start. BDTest records `Failed` with an `OperationCanceledException` and a
cancellation reason; framework cancellation alone does not set BDTest's `TimedOut` flag.
The enclosing framework may also report its own timeout or cancellation outcome.

Use the framework-specific factory for automatic forwarding. A core-only
`BDTestScenario.For<TContext>(this, run)` host has no framework token; pass the runner token
to `BDTestAsync(token)` explicitly when retaining that host. Steps must observe their token;
BDTest cannot interrupt arbitrary synchronous code. See [timeouts](../writing-tests/timeouts.md).
