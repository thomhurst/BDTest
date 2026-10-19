---
title: xUnit
description: Use BDTest with xUnit v3 through composition or an optional base class.
---

# xUnit

The adapter uses a [configured assembly run](../configuration/assembly-runs.md). Configure `BDTestOptions` with `[assembly: BDTestConfiguration(typeof(TestConfiguration))]`. Use `XUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly)` for reports and call `XUnitBDTestAssembly.Dispose(assembly)` after all tests and exports finish. Explicit-run overrides remain supported.


```bash
dotnet add package BDTest.xUnit
```

`BDTest.xUnit` targets `netstandard2.0;net8.0` and requires **xUnit v3**. Existing xUnit v2 projects must migrate before upgrading; see [Migrating from xUnit v2](#migrating-from-xunit-v2).

Use **Microsoft.Testing.Platform (MTP)** for xUnit v3. Reference `xunit.v3`, make the project executable, and enable its MTP command-line runner:

```xml title="MyApp.AcceptanceTests.csproj"
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="BDTest.xUnit" Version="*" />
  <PackageReference Include="BDTest.SourceGenerator" Version="*" PrivateAssets="all" />
  <PackageReference Include="xunit.v3" Version="4.0.2" />
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="2.5.1" />
</ItemGroup>
```

Run the project directly, with optional TRX reporting or a class filter:

```bash
dotnet run --project MyApp.AcceptanceTests -c Release
dotnet run --project MyApp.AcceptanceTests -c Release -- --report-trx --results-directory artifacts/test-results
dotnet run --project MyApp.AcceptanceTests -c Release -- --filter-class '*MyTests'
```

`Microsoft.Testing.Extensions.TrxReport` supplies `--report-trx`; omit it if you do not need TRX reports. MTP projects do not need `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` or `coverlet.collector`. For coverage, use an MTP extension such as `Microsoft.Testing.Extensions.CodeCoverage`. See [xUnit's runner configuration](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).

BDTest preserves console capture from constructors through scenario execution during parallel MTP runs, including on Linux. Console-only summaries remain outside captured scenario output.

### VSTest fallback

If your tooling requires VSTest, keep `<OutputType>Exe</OutputType>`, remove `UseMicrosoftTestingPlatformRunner` and the MTP reporting extension, and replace `xunit.v3` with these references:

```xml
<PackageReference Include="xunit.v3.mtp-off" Version="4.0.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
<PackageReference Include="xunit.runner.visualstudio" Version="4.*" PrivateAssets="all" />
```

Run that configuration with `dotnet test MyApp.AcceptanceTests.csproj`. The BDTest adapter works with either runner.

## Composition with an unrelated base class

Register the native lifecycle fixture once per assembly, then create a host in your
constructor. Supply the actual test instance and an explicit, caller-owned run:

```csharp sample="XUnit/Composition.cs" title="Compiled example"
using BDTest;
using BDTest.Helpers;
using Xunit;

[assembly: AssemblyFixture(typeof(XUnitBDTestLifecycle))]
[assembly: AssemblyFixture(typeof(CheckoutRun))]

public sealed class CheckoutRun : IAsyncDisposable
{
    public BDTestRun Run { get; }

    public CheckoutRun()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(XUnitBDTestScenario.OutcomeMap);
        // Configure retries, context factories and hooks before freezing options.
        Run = BDTestRun.Start(options);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await File.WriteAllTextAsync("checkout.html",
                BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), Run));
        }
        finally { Run.Dispose(); }
    }
}

public class CheckoutTests : CompanyTestBase, IAsyncLifetime
{
    private readonly BDTestScenario<CheckoutContext> _bdt;

    public CheckoutTests(CheckoutRun owner)
    {
        _bdt = XUnitBDTestScenario.For<CheckoutContext>(this, owner.Run);
        Console.WriteLine("Preparing checkout");
    }

    public async ValueTask InitializeAsync() => await _bdt.InitializeContextAsync();

    [Fact]
    public async Task Pays()
    {
        await _bdt.When(() => _bdt.Context.Pay())
            .Then(() => _bdt.Context.VerifyReceipt()).BDTestAsync(Xunit.TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Console.WriteLine("Checkout teardown");
        return default;
    }
}
```

`XUnitBDTestScenario.For` binds synchronously before asynchronous setup. It supplies
xUnit's exact test method and unique execution ID, including runtime-enumerated
theory rows. Story, tags, scenario text, and attributed retry hooks belong to your
test instance. Read `Context` from the host inside steps so retries see the new context.

The registered fixture uses xUnit's `INotifyTestLifecycle` and
`INotifyTestLifecycleAsync` extension points. It awaits host completion after user
teardown, including constructor, setup, body, and teardown failures. You do not call
`BeginTest`, `EndTest`, or inherit a BDTest class. Registering the fixture does not
create or configure a run. It can complete hosts from several explicit runs in one
assembly; each run retains its own contexts, output, options, and reports. Runs must
outlive all test cleanup. An assembly fixture is a convenient owner for explicit runs. Alternatively, `XUnitBDTestScenario.For<TContext>(this)` selects the configured assembly run; retain the `XUnitBDTestLifecycle` registration.

Create only one composition host per native test and explicit run, regardless of
context type. A second call for the same test and run throws before changing
capture or attachment state, including after the first host completes. Reuse the
host for that test. Hosts in different runs remain independent.

Contexts created by `BDTestOptions.ContextFactory` do not need a public parameterless
constructor. Passing a different object from inside a running test throws an
`ArgumentException` for `testInstance`; pass the actual test instance (`this`).

Configure the outcome map before `BDTestRun.Start` for framework exceptions and
whole-scenario skips. Missing lifecycle registration throws when creating the host.
Hosts use the [core composition API](composition.md), including scenario outlines
and output helpers. `EndTestAsync()` is available for custom/manual integrations
that must await context disposal; native xUnit integration calls it for you.

To migrate an existing fixture, keep its unrelated base class, replace inherited
builders and `Context` reads with host members, and move run settings into options
before starting the run. Replace adapter lifecycle overrides with ordinary xUnit
`IAsyncLifetime` methods. Remove manual BDTest start/completion calls. The adapter's
target frameworks and xUnit v3 runner requirements remain unchanged.

## Writing tests with the optional base class

Inherit from `XUnitBDTestBase<TContext>`. The base class has a parameterless constructor:

```csharp sample="XUnit/Inheritance.cs" title="Compiled example"
using BDTest;
using Xunit;

[Story(AsA = "shopper", IWant = "to add items to my basket", SoThat = "I can buy them")]
public class BasketTests : XUnitBDTestBase<BasketContext>
{
    private BasketSteps Basket => new(Context);

    [Fact]
    [ScenarioText("Adding an item updates the total")]
    public async Task AddingAnItem()
    {
        await Given(() => Basket.IsEmpty())
            .When(() => Basket.Add("Book", 12.50m))
            .Then(() => Basket.TheTotalIs(12.50m))
            .BDTestAsync(Xunit.TestContext.Current.CancellationToken);
    }
}
```

The base class uses the same binding, capture and completion path as composition.
Register `XUnitBDTestLifecycle` for fallback cleanup even when a constructor, setup,
or teardown override throws before calling base. Existing fixtures without the
registration retain their explicit base-class lifecycle behavior.

Contexts may have constructor dependencies: override `CreateContextAsync(ContextCreation)` to construct them. BDTest awaits `IBDTestContextLifetime.InitializeAsync()` before scenario steps and disposes owned contexts at teardown, preferring `IAsyncDisposable` over `IDisposable`. Call `await InitializeContextAsync()` when async fixture setup needs an initialized context before any scenario. See [Test context](../writing-tests/test-context.md) for factories, retries and ownership.

The adapter uses the protected [constructor-output API](custom-adapters.md#constructor-output-and-assertion-scopes), so it requires no friend access to core internals. Constructor output binds only after the derived fixture has supplied its owned run.

## What the adapter does

- **Execution ID:** `TestContext.Current.Test.UniqueID`. Theory rows get separate IDs and contexts, even when their data is not serializable.
- **Exceptions:** registers `Xunit.Sdk.SkipException` (from `Assert.Skip(...)`) as a skip exception.
- **Start:** the class implements `IAsyncLifetime`. Its initialization hook calls `BeginTest` after your constructor finishes, then calls the virtual `InitializeAsync()`.
- **Teardown:** `DisposeAsync()` awaits context disposal, completes the BDTest execution and clears pending output and listener state.
- **Skips:** a whole-scenario skip calls `Assert.Skip(reason)`, so BDTest and the xUnit runner both report the test as skipped.
- **Output:** BDTest captures console output itself. To also write to xUnit's output, inject `Xunit.ITestOutputHelper` into your own constructor.

## Initialization and constructor output

Console output from constructors is buffered until the initialization hook runs. A [`TestRun`](../advanced/explicit-runs.md) assigned in your constructor therefore owns that output.

For async setup, override `InitializeAsync()`. You do not need to call the base method; capture is already active when your override starts. Prefer this override to reimplementing `IAsyncLifetime`.

```csharp title="Illustrative fragment"
public override async ValueTask InitializeAsync()
{
    await Context.OpenAsync();
}
```

Fixtures that reimplement `IAsyncLifetime.InitializeAsync()` still work: their constructor and setup output stays buffered until an inherited before-test hook binds the execution. Disposal also binds the execution when setup fails before that hook runs, then removes its context and pending output. Tasks started in constructors share the buffer: writes after binding go to the owning run, and writes after disposal are ignored by BDTest capture.

## Asynchronous teardown

xUnit v3 calls `DisposeAsync()` instead of `Dispose()` for test classes that implement `IAsyncDisposable` or `IAsyncLifetime`. Override the adapter's virtual `DisposeAsync()` for asynchronous teardown, and always await the base method in a `finally` block so BDTest cleanup still runs when your teardown fails:

```csharp title="Illustrative fragment"
public override async ValueTask DisposeAsync()
{
    try
    {
        await Context.CloseAsync();
    }
    finally
    {
        await base.DisposeAsync();
    }
}
```

If you hide the method or explicitly reimplement `IAsyncDisposable.DisposeAsync()`, without native lifecycle registration you must still call `await base.DisposeAsync()`; xUnit does not also call `Dispose()`. See the [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration#iasynclifetime-now-inherits-from-iasyncdisposable).

Existing overrides of the obsolete `MarkTestAsComplete()` and reimplementations of `IDisposable` retain their synchronous teardown path for compatibility. Such overrides must call the base method to complete BDTest cleanup. Use `DisposeAsync()` for new teardown code.

## Reports after the run

Add `BDTest.Reporting` at the same version as the adapter before using these export APIs.
See [reporting package migration](../migration/reporting-package.md).

Use an xUnit v3 assembly fixture. xUnit disposes it after every test in the assembly has finished:

```csharp sample="XUnit/AssemblyReport.cs" title="Compiled example"
using BDTest.Helpers;
using Xunit;

[assembly: AssemblyFixture(typeof(BDTestReports))]

public sealed class BDTestReports : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var assembly = typeof(BDTestReports).Assembly;
        try
        {
            var run = BDTest.XUnitBDTestAssembly.GetRun(assembly);
            await File.WriteAllTextAsync("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport(null, run));
        }
        finally { BDTest.XUnitBDTestAssembly.Dispose(assembly); }
    }
}
```

## Migrating from xUnit v2

1. Replace the `xunit` 2.x package with `xunit.v3`, set `<OutputType>Exe</OutputType>` and `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`, and remove `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` and `coverlet.collector`. Run with `dotnet run --project <project>`. Add the MTP TRX extension above if you need reports. If your tooling still requires VSTest, use the [VSTest fallback](#vstest-fallback) instead.
2. Remove constructors that only passed `ITestOutputHelper` to `base(...)`. If you still need the helper, inject `Xunit.ITestOutputHelper` (not `Xunit.Abstractions.ITestOutputHelper`) in your derived class.
3. Change existing `DisposeAsync()` and `InitializeAsync()` implementations to overrides; await `base.DisposeAsync()` in teardown.
4. Remove workarounds for runtime skips: BDTest skip rules now produce skipped xUnit results instead of throwing `NotSupportedException`.

The adapter supplies the runner's exact test method to `BeginTest(id, methodInfo)`. Scenario text, information, tags and retry attributes use cached method metadata, including when a helper constructs the scenario after an await. See [custom adapter migration](custom-adapters.md#method-identity-and-migration).
