---
title: Installation
description: Install BDTest and the adapter for your test framework from NuGet.
---

# Installation

BDTest is distributed on [NuGet](https://www.nuget.org/packages/BDTest). Install the adapter for your test framework. Each adapter depends on the core `BDTest` package, so you get both.

```bash
# TUnit (recommended)
dotnet add package TUnit
dotnet add package BDTest.TUnit

# NUnit
dotnet add package BDTest.NUnit

# xUnit v3 (Microsoft.Testing.Platform)
dotnet add package xunit.v3
dotnet add package BDTest.xUnit

# MSTest
dotnet add package BDTest.MSTest
```

Then add the source generator to every project that contains steps. It lets BDTest render `[StepText]`, method names and argument values in reports; without it, steps show their source text. See [Step text](../writing-tests/step-text.md#install-the-source-generator).

```bash
dotnet add package BDTest.SourceGenerator
```

Mark it `PrivateAssets="all"` in the project file so it does not flow to projects that reference yours.

Add optional compile-time checks and IDE fixes with `dotnet add package BDTest.Analyzers`, also marked `PrivateAssets="all"`. See [Analyzers and code fixes](../advanced/analyzers.md) for rule IDs, installation and suppression.

To use BDTest without a framework adapter, install only the core package (plus the generator):

```bash
dotnet add package BDTest
```

For JSON/HTML export and HTTP uploads, add `BDTest.Reporting` alongside your adapter:

```bash
dotnet add package BDTest.Reporting
```

See [reporting package migration](../migration/reporting-package.md). Execution and
console output need no reporting reference.

For independent immutable report DTOs, add `BDTest.Contracts`. It has no dependency
on the execution package. See [report contracts](../reports/contracts.md) for its
versioned JSON shape and the current migration boundary.

For a report server, add `BDTest.Razor.Reports` to an ASP.NET Core project. Add
`BDTest.Reports.Storage.FileSystem` for persistent directory-based storage. Both
target .NET 10. See [Report server setup](../report-server/setup.md) and
[Persistence](../report-server/persistence.md#file-system-package).

:::tip Keep versions aligned
Use the same version for every BDTest package in a solution. The adapters, the report server and the core package share data models.
:::

## Package naming policy

The package IDs in the table below stay unchanged for the next major version.
`BDTest.xUnit` retains its existing casing and supports xUnit v3.
`BDTest.Razor.Reports` uses the display title **BDTest - ASP.NET Core Reports**;
its package ID, namespaces and static asset paths remain unchanged. No package
rename, replacement reference or NuGet deprecation is required.

`BDTest.ReportGenerator.RazorServer` remains an unpacked sample application.
Storage packages use the `BDTest.Reports.Storage.*` prefix.
`BDTest.Reports.Storage.FileSystem` provides directory-based persistence;
`BDTest.Reports.Storage.AzureBlob` provides [Azure Blob persistence](../report-server/persistence.md#azure-and-memory-implementations). Custom stores use the
[existing storage contracts](../report-server/persistence.md).

## Assembly versions

Starting with version 3.0.0, including prereleases, strong-named assemblies use
`Major.0.0.0` as their assembly version. Minor and patch releases within that
major keep the same assembly identity. Package versions and file versions still
identify each release; informational versions and report version fields include
the full release version and source revision. The 2.x series keeps its existing
assembly version policy. Keep package versions aligned as described above, and
review migration notes when upgrading to a new major version.

## Supported targets

The core `BDTest` package targets `netstandard2.0;net8.0`. Adapter and report server requirements differ:

| Package | Target framework | Test framework dependency |
| --- | --- | --- |
| `BDTest` | `netstandard2.0;net8.0` | None |
| `BDTest.Reporting` | `netstandard2.0;net8.0` | None |
| `BDTest.Contracts` | `netstandard2.0` | None |
| `BDTest.TUnit` | `netstandard2.0;net8.0` | TUnit |
| `BDTest.NUnit` | `net8.0` | NUnit 5 |
| `BDTest.xUnit` | `netstandard2.0;net8.0` | xUnit v3 |
| `BDTest.MSTest` | `netstandard2.0;net8.0` | MSTest 4 |
| `BDTest.Razor.Reports` | `net10.0` | ASP.NET Core |
| `BDTest.Reports.Storage.FileSystem` | `net10.0` | ASP.NET Core report storage |
| `BDTest.Reports.Storage.AzureBlob` | `net10.0` | Azure Blob Storage / ASP.NET Core |
| `BDTest.SourceGenerator` | `netstandard2.0` analyzer | C# 10+ compiler (.NET SDK 8+) |
| `BDTest.Analyzers` | `netstandard2.0` analyzer | Roslyn 4.8+ (.NET SDK 8+) |

BDTest's step builders take delegates and need a **C# 13 or newer** compiler (.NET SDK 9 or newer). The core package retains its `netstandard2.0` build alongside `net8.0`.

The policy preserves .NET Standard runtime assets and adds native .NET 8 assets wherever the adapter dependencies allow it. NUnit retains its .NET 8 floor; ASP.NET Core report hosting stays on .NET 10. Compiler extensions keep their .NET Standard compiler-host target and analyzer packaging. NuGet selects the .NET 8 build automatically for .NET 8+ consumers. See [Target framework migration](../migration/target-frameworks.md).

Choose a test project target that both the adapter and its test framework support. A `netstandard2.0` adapter does not guarantee that its test framework or runner supports .NET Framework. The NUnit adapter requires .NET 8 or later.

## Test project setup

A typical TUnit test project:

```xml title="MyApp.AcceptanceTests.csproj"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BDTest.TUnit" Version="*" />
    <PackageReference Include="BDTest.SourceGenerator" Version="*" PrivateAssets="all" />
    <PackageReference Include="BDTest.Analyzers" Version="*" PrivateAssets="all" />
    <PackageReference Include="TUnit" Version="*" />
  </ItemGroup>
</Project>
```

TUnit test projects are executables that run on Microsoft.Testing.Platform; do not add `Microsoft.NET.Test.Sdk`. Replace `*` with the versions you want to pin. Continue with [your first test](first-test.md).

For xUnit v3, use `xunit.v3` with `<OutputType>Exe</OutputType>` and `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`, then run `dotnet run --project <project>`. Do not add VSTest packages to this setup. See [xUnit setup](../frameworks/xunit.md) for the complete MTP configuration, optional TRX reporting, and a VSTest fallback.
