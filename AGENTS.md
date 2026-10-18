# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

BDTest is a code-only BDD testing and reporting framework for .NET (an alternative to SpecFlow, no Gherkin or generated code). Tests are written as `Given(...).When(...).Then(...).BDTestAsync()` chains; step/scenario/story text comes from `[StepText]`, `[ScenarioText]`, `[Story]` attributes or is humanized from method names. Usage docs live in the GitHub wiki: https://github.com/thomhurst/BDTest/wiki

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
3. Sets `TestOutputData.FrameworkExecutionId` before each test, and calls `RemoveContext()` / `MarkTestAsComplete()` after.

Core uses `InternalsVisibleTo` for `BDTest.NUnit`, `BDTest.xUnit`, `BDTest.TUnit` and the Razor middleware, with the public key from `StrongNameKeyPair.snk`. A new adapter that needs internals must be added to `BDTest/BDTest.csproj` and must also be signed.

**Reporting**
- `BDTest.Razor.Reports`: ASP.NET Core MVC controllers/views. Hosts add it with `AddBdTestReportControllersAndViews()` + `UseBDTestReportServer(...)`. Storage is pluggable through interfaces in `Interfaces/`.
- `BDTest.ReportGenerator.RazorServer`: deployable report server using that library, with an Azure Blob data store and Azure AD auth. Not packed.

**Tests / samples**
- `BDTest.Example`: sample NUnit tests, also referenced by `BDTest.Tests`.
- `BDTest.Tests` (NUnit), `BDTest.xUnit.Tests`, `BDTest.TUnit.Tests`: the framework-specific tests focus on unique-ID/context isolation under parallel execution.

**Pipeline (`BDTest.Pipeline/`)**: ModularPipelines app driven by GitHub Actions (`.github/workflows/dotnet.yml`).
- `RunUnitTestsModule` finds every `*.csproj` path containing "Tests". It uses `dotnet run` for TUnit projects and `dotnet test` for the rest.
- `PackProjectsModule` packs every csproj except paths containing Tests, Pipeline, Example or ReportGenerator. New package projects are picked up automatically by that naming rule.
- Development env pushes to a local NuGet folder. Production pushes to nuget.org, gated on `PUBLISH_PACKAGES`.

## Conventions

- `Directory.Build.props` sets `LangVersion=latest` and `ImplicitUsings=true` for all projects.
- Packable libraries target `netstandard2.0`. Some use `.nuspec` files (`NuspecProperties version=$(Version)`); `BDTest.TUnit` uses csproj package metadata.
- Dependency updates arrive via Renovate/Dependabot PRs and are auto-merged by workflow.
