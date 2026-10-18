---
title: xUnit
description: Use BDTest with xUnit v3 through XUnitBDTestBase<TContext>.
---

# xUnit

```bash
dotnet add package BDTest.xUnit
```

`BDTest.xUnit` targets `netstandard2.0` and requires **xUnit v3**. Existing xUnit v2 projects must migrate before upgrading; see [Migrating from xUnit v2](#migrating-from-xunit-v2).

To run with VSTest (`dotnet test`), reference `xunit.v3.mtp-off`, `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` 4.x, and set `<OutputType>Exe</OutputType>`:

```xml title="MyApp.AcceptanceTests.csproj"
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="BDTest.xUnit" Version="*" />
  <PackageReference Include="xunit.v3.mtp-off" Version="4.0.2" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="4.*" PrivateAssets="all" />
</ItemGroup>
```

Projects that use Microsoft.Testing.Platform instead can reference `xunit.v3` and follow [xUnit's runner configuration](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).

## Writing tests

Inherit from `XUnitBDTestBase<TContext>`. The base class has a parameterless constructor:

```csharp
using BDTest.Attributes;
using BDTest.xUnit;
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
            .BDTestAsync();
    }
}
```

## What the adapter does

- **Execution ID:** `TestContext.Current.Test.UniqueID`. Theory rows get separate IDs and contexts, even when their data is not serializable.
- **Exceptions:** registers `Xunit.Sdk.SkipException` (from `Assert.Skip(...)`) as a skip exception.
- **Start:** the constructor marks the test as started.
- **Teardown:** the class implements `IDisposable` and `IAsyncDisposable`. Disposal completes the BDTest execution, removes the context and clears pending output and listener state.
- **Skips:** a whole-scenario skip calls `Assert.Skip(reason)`, so BDTest and the xUnit runner both report the test as skipped.
- **Output:** BDTest captures console output itself. To also write to xUnit's output, inject `Xunit.ITestOutputHelper` into your own constructor.

## Asynchronous teardown

xUnit v3 calls `DisposeAsync()` instead of `Dispose()` for test classes that implement `IAsyncDisposable` or `IAsyncLifetime`. Override the adapter's virtual `DisposeAsync()` for asynchronous teardown, and always await the base method in a `finally` block so BDTest cleanup still runs when your teardown fails:

```csharp
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

This also applies when you implement `IAsyncLifetime.InitializeAsync()`. If you hide the method or explicitly reimplement `IAsyncDisposable.DisposeAsync()`, you must still call `await base.DisposeAsync()`; xUnit does not also call `Dispose()`. See the [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration#iasynclifetime-now-inherits-from-iasyncdisposable).

## Reports after the run

Use an xUnit v3 assembly fixture. xUnit disposes it after every test in the assembly has finished:

```csharp
using BDTest.Helpers;
using Xunit;

[assembly: AssemblyFixture(typeof(BDTestReports))]

public sealed class BDTestReports : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await File.WriteAllTextAsync("BDTest-Report.html", BDTestHtmlHelper.GetTestHtmlReport());
        // or: await BDTestReportServer.SendDataAndGetReportUriAsync(...)
    }
}
```

## Migrating from xUnit v2

1. Replace the `xunit` 2.x package with `xunit.v3.mtp-off` (to keep VSTest) or `xunit.v3`, and set `<OutputType>Exe</OutputType>`. Keep `Microsoft.NET.Test.Sdk` and update `xunit.runner.visualstudio` to 4.x.
2. Remove constructors that only passed `ITestOutputHelper` to `base(...)`. If you still need the helper, inject `Xunit.ITestOutputHelper` (not `Xunit.Abstractions.ITestOutputHelper`) in your derived class.
3. Change existing `DisposeAsync()` implementations to overrides that await `base.DisposeAsync()`.
4. Remove workarounds for runtime skips: BDTest skip rules now produce skipped xUnit results instead of throwing `NotSupportedException`.
