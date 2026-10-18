---
title: Installation
description: Install BDTest and the adapter for your test framework from NuGet.
---

# Installation

BDTest is distributed on [NuGet](https://www.nuget.org/packages/BDTest). Install the adapter for your test framework. Each adapter depends on the core `BDTest` package, so you get both.

```bash
# TUnit (recommended)
dotnet add package BDTest.TUnit

# NUnit
dotnet add package BDTest.NUnit

# xUnit v3
dotnet add package BDTest.xUnit

# MSTest
dotnet add package BDTest.MSTest
```

Then add the source generator to every project that contains steps. It lets BDTest render `[StepText]`, method names and argument values in reports; without it, steps show their source text. See [Step text](../writing-tests/step-text.md#install-the-source-generator).

```bash
dotnet add package BDTest.SourceGenerator
```

Mark it `PrivateAssets="all"` in the project file so it does not flow to projects that reference yours.

To use BDTest without a framework adapter, install only the core package (plus the generator):

```bash
dotnet add package BDTest
```

For a report server, add `BDTest.Razor.Reports` to an ASP.NET Core project. See [Report server setup](../report-server/setup.md).

:::tip Keep versions aligned
Use the same version for every BDTest package in a solution. The adapters, the report server and the core package share data models.
:::

## Supported targets

The core `BDTest` package targets `netstandard2.0`. Adapter and report server requirements differ:

| Package | Target framework | Test framework dependency |
| --- | --- | --- |
| `BDTest` | `netstandard2.0` | None |
| `BDTest.TUnit` | `netstandard2.0` | TUnit |
| `BDTest.NUnit` | `net8.0` | NUnit 5 |
| `BDTest.xUnit` | `netstandard2.0` | xUnit v3 |
| `BDTest.MSTest` | `netstandard2.0` | MSTest 4 |
| `BDTest.Razor.Reports` | `net10.0` | ASP.NET Core |
| `BDTest.SourceGenerator` | `netstandard2.0` analyzer | C# 10+ compiler (.NET SDK 8+) |

BDTest's step builders take delegates and need a **C# 13 or newer** compiler (.NET SDK 9 or newer). The core package still targets `netstandard2.0`.

Choose a test project target that both the adapter and its test framework support. A `netstandard2.0` adapter does not guarantee that its test framework or runner supports .NET Framework. The NUnit adapter requires .NET 8 or later.

## Test project setup

A typical TUnit test project:

```xml title="MyApp.AcceptanceTests.csproj"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BDTest.TUnit" Version="*" />
    <PackageReference Include="BDTest.SourceGenerator" Version="*" PrivateAssets="all" />
    <PackageReference Include="TUnit" Version="*" />
  </ItemGroup>
</Project>
```

TUnit test projects are executables that run on Microsoft.Testing.Platform; do not add `Microsoft.NET.Test.Sdk`. Replace `*` with the versions you want to pin. Continue with [your first test](first-test.md).
