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
dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj
dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj --filter "FullyQualifiedName~SomeTestName"

# Core, TUnit adapter, pipeline and report-generator suites use Microsoft.Testing.Platform
dotnet run --project BDTest.Tests
dotnet run --project BDTest.Tests -- --treenode-filter "/*/*/RetryTests/*"
dotnet run --project BDTest.TUnit.Tests
dotnet run --project BDTest.Pipeline.Tests
dotnet run --project BDTest.ReportGenerator.Tests
dotnet run --project BDTest.TUnit.Tests -- --treenode-filter "/*/*/TUnitScenarioTests/*"
```

The core `BDTest.Tests` suite uses TUnit with awaited assertions, per-test fixture
instances and explicit `BDTestRun` ownership. Its source-generator and analyzer
references remain active; it is the sole signed test friend assembly.
`BDTest.NUnit.Tests` owns NUnit adapter lifecycle, execution IDs, runner outcomes,
shared conformance and sample integration; `BDTest.Example` remains an NUnit sample.
`BDTest.NUnit.Tests` is unsigned and has no friend access to core or Razor reports. CI evaluates
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
| `BDTest.Reports.Storage.AzureBlob` | Packaged Azure report, run-index and attachment store. |
| `BDTest.ReportGenerator.RazorServer` | Sample report host (not packed). |
| `BDTest.SourceGenerator` | Optional compile-time step metadata. |
| `BDTest.Example` | Sample NUnit tests. |
| `BDTest.Tests` | Core and reporting tests (TUnit; signed friend assembly). |
| `BDTest.NUnit.Tests` | NUnit adapter and sample integration tests (unsigned). |
| `*.Tests` | Other framework, compiler, contract and tooling suites. |
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

### Compiling documentation examples

From the repository root, with Node 24 and .NET SDK 10.0.402 installed:

```bash
npm --prefix docs run check:samples
```

This command needs no npm install. It tests the extractor, reads the actual C#
fences in `README.md` and `docs/docs`, and builds the examples in Release against
project references to the current core, adapters, source generator and reports.
Warnings are errors, just as in the rest of the repository. The documentation
workflow runs this on every PR to `main` and every `main` push, including changes
outside `docs`, before building the site. It compiles examples; it does not start
report servers or upload results.

Code blocks labelled **Compiled example** participate in this check. The
first-test walkthrough's four files form one example. Adapter examples use the
small basket/checkout domain models in
[`docs/samples`](https://github.com/thomhurst/BDTest/tree/main/docs/samples);
these provide application state and assertions, never substitutes for BDTest APIs.
**Illustrative fragment** blocks omit surrounding code, depend on your own
application, show interface shapes, or include old/invalid code for comparison.
Use the compiled first-test and adapter examples as their supporting fixtures.
Fragments are not claimed to compile independently.

To add an executable example, give its fence a unique `sample="Project/File.cs"`
attribute and `title="Compiled example"`, using an existing sample project when
its dependencies fit. The checker extracts that fence verbatim into
`docs/samples/Project/obj/Examples/File.cs`; compiler errors point to the original
Markdown line. Do not edit the generated file. Add application-only helpers to
the project's `Support.cs` and explain them on the page if needed.

For a different dependency set, add a non-packable `docs/samples/Project/Project.csproj`
with a distinct assembly name, project references to the relevant libraries, and
`Compile Include="obj/Examples/*.cs"`. Existing projects show runner setup and
source-generator references. Every project must have at least one displayed
sample. Duplicate IDs, unknown projects and unclassified C# fences fail the check.
Mark intentionally incomplete or historical C# with `title="Illustrative fragment"`.
Use unindented triple-backtick fences immediately followed by `csharp`.
Alternate C# fences (including tildes, indentation and longer backtick fences)
are rejected so displayed examples cannot silently bypass the check.
Never relabel a broken executable example to bypass compilation; fix it and its
supporting code together. The README must retain its compiled quick start.

You can also run the isolated README and first-test examples without any report service:

```bash
npm --prefix docs run check:samples
dotnet run --project docs/samples/Readme -c Release --no-build
dotnet run --project docs/samples/FirstTest -c Release --no-build
```

When a major-version change lands, add its upgrade action and focused guide link
to [Upgrading to the next major version](migration/next-major.md) in the same PR.
Describe merged behavior separately from pending roadmap work.

## Runtime target validation

Runtime packages enable SDK package validation during `dotnet pack`, including the pipeline's `PackProjectsModule`. It compares compatible target assets for API consistency. Artifact verification evaluates dependencies independently for each target, including conditional references. Compiler extension packages contain analyzer DLLs, so runtime API validation does not apply to them.

`BDTest.Targets.Tests` exercises the modern core asset on .NET 8 and .NET 10. `BDTest.Legacy.Tests` links the same runtime contract tests but explicitly selects the .NET Standard asset on .NET 8. Both projects run in the normal test pipeline. The tests check consumer module initializers, timeout classification and cancellation, timestamps, and elapsed durations. Modern-only tests use a fake `TimeProvider` to drive timers without waiting for real time.
