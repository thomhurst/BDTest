---
title: Contributing
description: Build, test and document BDTest locally.
---

# Contributing

BDTest is developed on [GitHub](https://github.com/thomhurst/BDTest). Issues and pull requests are welcome.

## Building

Use the .NET 10 SDK (CI uses `10.0.402`).

```bash
dotnet build BDTest.sln
```

## Running tests

```bash
# NUnit, xUnit and MSTest projects use VSTest
dotnet test BDTest.Tests/BDTest.Tests.csproj
dotnet test BDTest.Tests/BDTest.Tests.csproj --filter "FullyQualifiedName~SomeTestName"

# TUnit adapter, pipeline and report-generator suites use Microsoft.Testing.Platform
dotnet run --project BDTest.TUnit.Tests
dotnet run --project BDTest.Pipeline.Tests
dotnet run --project BDTest.ReportGenerator.Tests
dotnet run --project BDTest.TUnit.Tests -- --treenode-filter "/*/*/TUnitScenarioTests/*"
```

The core `BDTest.Tests` suite still uses NUnit. CI evaluates
Release `PackageReference` items to choose the TUnit runner; filenames and
`OutputType=Exe` do not decide the runner. xUnit v3 keeps its VSTest invocation.
Both paths save TRX results under `artifacts/test-results/<project>`.

The full CI pipeline (tests, versioning, packing) is a [ModularPipelines](https://github.com/thomhurst/ModularPipelines) app:

```bash
cd BDTest.Pipeline
DOTNET_ENVIRONMENT=Development dotnet run -c Release
```

Use `Development` locally. `Production` can publish packages to NuGet.

## Repository layout

| Folder | Contents |
| --- | --- |
| `BDTest` | Core library (`netstandard2.0;net8.0`, strong-named). |
| `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit` | Framework adapters. |
| `BDTest.Razor.Reports` | Report server controllers and views. |
| `BDTest.ReportGenerator.RazorServer` | Sample report host (not packed). |
| `BDTest.SourceGenerator` | Optional compile-time step metadata. |
| `BDTest.Example` | Sample NUnit tests. |
| `*.Tests` | Test projects. |
| `BDTest.Pipeline` | CI pipeline. |
| `docs` | This documentation site. |

`Directory.Build.props` defaults `IsPackable` to `false`. Shipped projects explicitly
set `IsPackable=true`. The pipeline evaluates that property with `dotnet msbuild`
in Release configuration; directory and project names do not select packages.
`build/PackageMetadata.targets` applies shared package metadata and the icon only to
packable projects, after their properties are evaluated. Copyright has no fixed
year. Compiler extensions keep their analyzer payloads and disable symbol packages.

The complete package set is listed in [Installation](getting-started/installation.md#supported-targets).
Keep existing IDs, including `BDTest.xUnit` for xUnit v3 and `BDTest.Razor.Reports`
for ASP.NET Core report hosting. Future storage IDs use `BDTest.Reports.Storage.*`.
The sample server remains non-packable. When adding a shipped project, opt it in,
supply its package metadata, and update `PackageSelectionTests` and the artifact
verification script. Run `dotnet run --project BDTest.Pipeline.Tests -c Release` to check
selection and metadata, including SourceGenerator and Analyzers.

## Documentation

This site is built with [Docusaurus](https://docusaurus.io/) from the `docs` folder and deployed to GitHub Pages when changes reach `main`.

```bash
cd docs
npm ci
npm start        # local preview with hot reload
npm run build    # production build; fails on broken links
```

When you change how BDTest is used (public API, attributes, settings, adapter behaviour, report formats or report server options), update the matching pages in `docs/docs` in the same pull request. Add new pages to `docs/sidebars.js`.

## Runtime target validation

Runtime packages enable SDK package validation during `dotnet pack`, including the pipeline's `PackProjectsModule`. It compares compatible target assets for API consistency. Artifact verification evaluates dependencies independently for each target, including conditional references. Compiler extension packages contain analyzer DLLs, so runtime API validation does not apply to them.

`BDTest.Targets.Tests` exercises the modern core asset on .NET 8 and .NET 10. `BDTest.Legacy.Tests` links the same runtime contract tests but explicitly selects the .NET Standard asset on .NET 8. Both projects run in the normal test pipeline. The tests check consumer module initializers, timeout classification and cancellation, timestamps, and elapsed durations. Modern-only tests use a fake `TimeProvider` to drive timers without waiting for real time.
