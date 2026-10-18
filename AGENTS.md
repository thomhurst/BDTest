# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

BDTest is a code-only BDD testing and reporting framework for .NET (an alternative to SpecFlow, no Gherkin or generated code). Tests are written as `Given(...).When(...).Then(...).BDTestAsync()` chains; step/scenario/story text comes from `[StepText]`, `[ScenarioText]`, `[Story]` attributes or is humanized from method names. Usage docs live in the Docusaurus site under `docs/` (published to https://thomhurst.github.io/BDTest/).

## Commands

SDK: .NET 10 (CI uses `10.0.402`). Solution: `BDTest.sln`.

```bash
dotnet build BDTest.sln

# NUnit / xUnit test projects (VSTest-based)
dotnet test BDTest.Tests/BDTest.Tests.csproj
dotnet test BDTest.Tests/BDTest.Tests.csproj --filter "FullyQualifiedName~SomeTestName"

# TUnit test project runs on Microsoft.Testing.Platform — use dotnet run, not dotnet test
dotnet run --project BDTest.TUnit.Tests
dotnet run --project BDTest.TUnit.Tests -- --treenode-filter "/*/*/TUnitScenarioTests/*"

# Documentation site (Docusaurus, Node 24)
cd docs && npm ci && npm run build   # build fails on broken links
cd docs && npm start                 # local preview

# Full CI pipeline (tests, version, pack, publish) — ModularPipelines
cd BDTest.Pipeline && dotnet run -c Release
```

`clean.cmd` deletes all `bin`/`obj` dirs. Use the pipeline above to build and pack all package projects. Set `DOTNET_ENVIRONMENT=Development` for local runs; `Production` can publish packages to NuGet.

## Architecture

**Core library (`BDTest/`, netstandard2.0, strong-named)**
- `Test/BDTestBase` exposes `Given/When/Then` builders (`Test/Steps/*`) that produce a `BuildableTest`, which becomes a `Scenario` of `Step`s when `BDTestAsync()` runs.
- `Test/AbstractContextBDTestBase<TContext>` adds a per-test `Context` object, keyed by the framework's execution ID (`BDTestExecutionId`). Context is recreated on retry.
- `Engine/ScenarioExecutor` runs scenarios (skip checks, step execution, exception classification, retries via `ScenarioRetryManager`).
- `Maps/TestHolder` is the static, process-wide store of all scenarios (by internal ID and by framework ID) plus not-run tests and the current report ID. `BDTestUtil` is the public read API over it.
- `Output/` captures console output per test (`ConsoleOutputInterceptor`, `TestOutputData`, keyed by `FrameworkExecutionId`).
- `Settings/BDTestSettings` is global static config, including `CustomExceptionSettings` (which exception types count as success / inconclusive).
- `BDTestReportServer.SendDataAndGetReportUriAsync` POSTs all collected scenarios (`BDTestOutputModel`) to a report server at `bdtest/data`.

**Framework adapters** (`BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit`): each is one thin base class (e.g. `TUnitBDTestBase<TContext>`) deriving from `AbstractContextBDTestBase<TContext>`. Each one:
1. Supplies `BDTestExecutionId` from the framework's test context.
2. Registers framework-specific skip/ignore/success exceptions into `BDTestSettings.CustomExceptionSettings`.
3. Calls `BeginTest(frameworkTestId)` before async setup and `EndTest()` after the test. xUnit buffers constructor output and binds the execution during initialization. Existing lifecycle overrides remain supported as compatibility shims.

Core and Razor reports grant `InternalsVisibleTo` only to `BDTest.Tests`, using SDK items and the single `BDTestPublicKey` in `Directory.Build.props`. Official and third-party adapters use the documented protected lifecycle, constructor-output and assertion-scope API; do not add adapter friend grants. Signing is centralized in `Directory.Build.props`: shipped runtime packages and the friend test assembly use the existing `StrongNameKeyPair.snk`; compiler extensions (`BDTest.SourceGenerator`, `BDTest.Analyzers`), pipeline, samples, report host and other tests stay unsigned. See `BUILDING.md` for verification.

**Reporting**
- `BDTest.Razor.Reports`: ASP.NET Core MVC controllers/views. Hosts add it with `AddBDTestReports(...)` + `UseBDTestReportServer()`. Storage is pluggable through interfaces in `Interfaces/`.
- `BDTest.ReportGenerator.RazorServer`: deployable report server using that library, with an Azure Blob data store and Azure AD auth. Not packed.

**Tests / samples**
- `BDTest.Example`: sample NUnit tests, also referenced by `BDTest.Tests`.
- `BDTest.Tests` (NUnit), `BDTest.xUnit.Tests`, `BDTest.TUnit.Tests`: the framework-specific tests focus on unique-ID/context isolation under parallel execution.

**Pipeline (`BDTest.Pipeline/`)**: ModularPipelines app driven by GitHub Actions (`.github/workflows/dotnet.yml`).
- `RunUnitTestsModule` finds every `*.csproj` path containing "Tests". It uses `dotnet run` for TUnit projects and `dotnet test` for the rest.
- `PackProjectsModule` packs every csproj except paths containing Tests, Pipeline, Example or ReportGenerator. New package projects are picked up automatically by that naming rule.
- Development env pushes to a local NuGet folder. Production pushes to nuget.org, gated on `PUBLISH_PACKAGES`.

## Conventions

- `Directory.Build.props` sets `LangVersion=latest` and `ImplicitUsings=enable` for all projects.
- Nullable analysis and warnings-as-errors currently apply to `BDTest.Pipeline` and `BDTest.Pipeline.Tests`; see `BUILDING.md` for the rollout plan.
- Package metadata is generated from csproj properties and `PackageReference`s. Shared metadata, the packed icon, and SourceLink/symbol settings live in `Directory.Build.props`; each package supplies its ID, title, description, and tags. Preserve each project's target framework.
- Keep the docs in sync with the library. Any change to how BDTest is used (public API, attributes, settings, adapter behavior, report formats, report server options or endpoints, package names or targets) must update the matching pages in `docs/docs/` in the same PR. Add new pages to `docs/sidebars.js`. Run `npm run build` in `docs/` to check links. `.github/workflows/docs.yml` builds the site on PRs and deploys it to GitHub Pages from `main`.
- Dependency updates arrive via Renovate/Dependabot PRs and are auto-merged by workflow.
