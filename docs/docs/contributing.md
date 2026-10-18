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

# TUnit uses Microsoft.Testing.Platform
dotnet run --project BDTest.TUnit.Tests
dotnet run --project BDTest.TUnit.Tests -- --treenode-filter "/*/*/TUnitScenarioTests/*"
```

The full CI pipeline (tests, versioning, packing) is a [ModularPipelines](https://github.com/thomhurst/ModularPipelines) app:

```bash
cd BDTest.Pipeline
DOTNET_ENVIRONMENT=Development dotnet run -c Release
```

Use `Development` locally. `Production` can publish packages to NuGet.

## Repository layout

| Folder | Contents |
| --- | --- |
| `BDTest` | Core library (`netstandard2.0`, strong-named). |
| `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit` | Framework adapters. |
| `BDTest.Razor.Reports` | Report server controllers and views. |
| `BDTest.ReportGenerator.RazorServer` | Sample report host (not packed). |
| `BDTest.SourceGenerator` | Optional compile-time step metadata. |
| `BDTest.Example` | Sample NUnit tests. |
| `*.Tests` | Test projects. |
| `BDTest.Pipeline` | CI pipeline. |
| `docs` | This documentation site. |

Every csproj outside paths containing `Tests`, `Pipeline`, `Example` or `ReportGenerator` is packed automatically.

## Documentation

This site is built with [Docusaurus](https://docusaurus.io/) from the `docs` folder and deployed to GitHub Pages when changes reach `main`.

```bash
cd docs
npm ci
npm start        # local preview with hot reload
npm run build    # production build; fails on broken links
```

When you change how BDTest is used (public API, attributes, settings, adapter behaviour, report formats or report server options), update the matching pages in `docs/docs` in the same pull request. Add new pages to `docs/sidebars.js`.
