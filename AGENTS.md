# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

BDTest is a code-only BDD testing and reporting framework for .NET (an alternative to SpecFlow, no Gherkin or generated code). Tests are written as `Given(...).When(...).Then(...).BDTestAsync()` chains; step/scenario/story text comes from `[StepText]`, `[ScenarioText]`, `[Story]` attributes or is humanized from method names. Usage docs live in the Docusaurus site under `docs/` (published to https://thomhurst.github.io/BDTest/).

## Commands

SDK: .NET 10 (CI uses `10.0.402`). Solution: `BDTest.sln`.

```bash
dotnet build BDTest.sln

# NUnit / MSTest test projects (VSTest-based)
dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj
dotnet test BDTest.NUnit.Tests/BDTest.NUnit.Tests.csproj --filter "FullyQualifiedName~SomeTestName"

# xUnit and TUnit suites use Microsoft.Testing.Platform (dotnet run)
dotnet run --project BDTest.Tests
dotnet run --project BDTest.Tests -- --treenode-filter "/*/*/RetryTests/*"
dotnet run --project BDTest.xUnit.Tests
dotnet run --project BDTest.xUnit.Tests -- --filter-class "*LifecycleTests"
dotnet run --project BDTest.TUnit.Tests
dotnet run --project BDTest.Pipeline.Tests
dotnet run --project BDTest.ReportGenerator.Tests
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
- `BDTestRun` owns `Maps/TestRunStore`, the executor, output and report ID. `BDTestUtil` requires the owning run to read or clear scenarios.
- `Output/` captures console output per test (`ConsoleOutputInterceptor`, `TestOutputData`, keyed by `FrameworkExecutionId`).
- `BDTestOptions` configures exception types, rules, converters and hooks; each run freezes its registrations at startup. There is no global settings object or default run.
- `BDTestReportServer.SendDataAndGetReportUriAsync` POSTs the selected run's collected scenarios (`BDTestOutputModel`) to a report server at `bdtest/data`.

**Framework adapters** (`BDTest.NUnit`, `BDTest.xUnit`, `BDTest.MSTest`, `BDTest.TUnit`): each is one thin base class (e.g. `TUnitBDTestBase<TContext>`) deriving from `AbstractContextBDTestBase<TContext>`. Each one:
1. Supplies `BDTestExecutionId` from the framework's test context.
2. Registers framework outcomes in the configured assembly run's `BDTestOptions`.
3. Calls `BeginTest(frameworkTestId)` before async setup and `EndTest()` after the test. xUnit buffers constructor output and binds the execution during initialization. Existing lifecycle overrides remain supported as compatibility shims.

Core and Razor reports grant `InternalsVisibleTo` only to `BDTest.Tests`, using SDK items and the single `BDTestPublicKey` in `Directory.Build.props`. Official and third-party adapters use the documented protected lifecycle, constructor-output and assertion-scope API; do not add adapter friend grants. Signing is centralized in `Directory.Build.props`: shipped runtime packages and the friend test assembly use the existing `StrongNameKeyPair.snk`; compiler extensions (`BDTest.SourceGenerator`, `BDTest.Analyzers`), pipeline, samples, report host and other tests stay unsigned. See `BUILDING.md` for verification.

**Reporting**
- `BDTest.Razor.Reports`: ASP.NET Core MVC controllers/views. Hosts add it with `AddBDTestReports(...)` + `UseBDTestReportServer()`. Storage is pluggable through interfaces in `Interfaces/`.
- `BDTest.ReportGenerator.RazorServer`: deployable report server using that library, with an Azure Blob data store and Azure AD auth. Not packed.

**Tests / samples**
- `BDTest.Tests`, `BDTest.Pipeline.Tests` and `BDTest.ReportGenerator.Tests` use TUnit and Microsoft.Testing.Platform.
- `BDTest.Example`: sample NUnit tests, referenced by `BDTest.NUnit.Tests`.
- `BDTest.Tests`: core and report behavior coverage using TUnit assertions, per-test fixture instances and explicit runs. It retains the sole signed test friend grant and compiler-generator coverage.
- `BDTest.NUnit.Tests`: unsigned NUnit adapter, lifecycle, runner outcomes, shared conformance and sample integration coverage.
- `BDTest.xUnit.Tests`, `BDTest.MSTest.Tests`, `BDTest.TUnit.Tests`: coverage on each adapter's native framework, including unique-ID/context isolation under parallel execution.

**Pipeline (`BDTest.Pipeline/`)**: ModularPipelines app driven by GitHub Actions (`.github/workflows/dotnet.yml`).
- `RunUnitTestsModule` finds every `*.csproj` path containing "Tests". It evaluates Release package references and `UseMicrosoftTestingPlatformRunner`, using `dotnet run` for TUnit and opted-in xUnit v3 projects and `dotnet test` for the rest. Both paths write TRX results under `artifacts/test-results/<project>`.
- `PackProjectsModule` packs every csproj except paths containing Tests, Pipeline, Example or ReportGenerator. New package projects are picked up automatically by that naming rule.
- Development env pushes to a local NuGet folder. Production pushes to nuget.org, gated on `PUBLISH_PACKAGES`.

## Conventions

- `Directory.Build.props` sets `LangVersion=latest` and `ImplicitUsings=enable` for all projects.
- `Nullable=enable` and `TreatWarningsAsErrors=true` apply to every project through `Directory.Build.props`; see `BUILDING.md` for nullable contract and validation guidance.
- Package metadata is generated from csproj properties and `PackageReference`s. Shared metadata, the packed icon, and SourceLink/symbol settings live in `Directory.Build.props`; each package supplies its ID, title, description, and tags. Preserve each project's target framework.
- Keep the docs in sync with the library. Any change to how BDTest is used (public API, attributes, settings, adapter behavior, report formats, report server options or endpoints, package names or targets) must update the matching pages in `docs/docs/` in the same PR. Add new pages to `docs/sidebars.js`. Run `npm run build` in `docs/` to check links. `.github/workflows/docs.yml` builds the site on PRs and deploys it to GitHub Pages from `main`.
- Dependency updates arrive via Renovate/Dependabot PRs and are auto-merged by workflow.
