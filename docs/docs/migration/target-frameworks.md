---
title: Target frameworks
description: Native .NET 8 runtime assets, retained .NET Standard support, nullable contracts, and private compiler polyfills.
---

# Target framework migration

In the next major version, `BDTest`, `BDTest.xUnit`, `BDTest.MSTest`, and `BDTest.TUnit` target `netstandard2.0;net8.0`. NuGet selects the .NET 8 asset for applications targeting .NET 8 or later. Existing older targets continue to select the .NET Standard asset. No existing runtime floor is raised by this change.

The authoritative [package matrix](../getting-started/installation.md#supported-targets) includes the exceptions: NUnit 5 requires .NET 8, report hosting uses ASP.NET Core 10, and the source generator and analyzers remain .NET Standard compiler components under `analyzers/dotnet/cs`. Check the test framework and runner's own runtime support independently of the adapter asset.

## Compiler attributes

The .NET Standard `ModuleInitializerAttribute` polyfill is now internal. The modern asset uses the runtime's attribute. Consumers can write `[System.Runtime.CompilerServices.ModuleInitializer]` without CS0433. Code that previously used BDTest's public polyfill must use the runtime attribute or supply its own internal polyfill on older targets. No compiler polyfill is exposed as a public BDTest type.

The modern build also uses the runtime's `CallerArgumentExpressionAttribute`; legacy compiler attributes remain internal. Consumer compiler requirements for delegate overload selection remain C# 13 or later.

## Execution and reports

The modern build uses `Task.WaitAsync` for timeouts and `TimeProvider.System` for UTC timestamps, monotonic elapsed timing, and timeout timers. Explicit runs can configure a [TimeProvider](../advanced/explicit-runs.md#execution-clock-net-8). The .NET Standard asset keeps the existing system-clock implementation. User exceptions, cancellation, retries, and late completion retain their behavior on both assets.

Both assets retain the source-generated `System.Text.Json` serialization paths and the same JSON package version. Historical report support is unchanged.

## Nullable reference contracts

The core library, NUnit, xUnit, MSTest and TUnit adapters, and Razor reports now publish nullable reference annotations on every supported target. These annotations describe existing optional values and do not change runtime signatures, target frameworks or report JSON. Consumers with nullable analysis enabled may see new warnings where a value can already be absent.

- `BDTestOptions.ContextFactory` and `Services`, and `ContextCreation.Services`, are optional. Check the service provider before using it in a custom factory. Factory methods must return a non-null context.
- Caller member, file and argument-expression parameters accept `null` when compiler metadata is unavailable. Custom builder wrappers should preserve those nullable parameters and forward their values unchanged.
- `BDTestContext.CurrentScenario` can be `null` before a scenario exists, including while an outline row is being constructed. `Scenario.Run` is `null` in deserialized reports. `Scenario.BdTestBaseClass` can also be `null` after its fixture is collected.
- Scenario, step and exception report fields that can be absent now use nullable types. Check optional text, identifiers and exceptions before dereferencing them; see [JSON data](../reports/json-data.md#nullable-report-fields).
- The MSTest adapter's injected `TestContext` is nullable before runner initialization and in manual harnesses. Check it before reading runner metadata.
- Reporting options keep nullable authorization policies and upload keys. `AddBDTestReports(null)` uses default options. Custom link text/URLs, absent query values and optional view-model navigation can be null.
- `IBDTestDataStore.GetTestData` returns `Task<BDTestReport?>`; legacy `StoreTestData` accepts `BDTestReport?`. Update custom implementations to match these annotations. Missing report/attachment streams also return null; see [storage migration](report-storage.md).
- `TestRunSummary` and `RunIndexEntry` identifiers, counts and optional metadata can be null in stored JSON. Omitted counts still default to an empty counts object. Check them before dereferencing; writes still need a record ID.
- `BDTestJsonHelper.Deserialize` and `DeserializeAsync` return `null` for a JSON `null` root. Check the result before reading report fields.

On the .NET 8 asset, the protected `BDTestException` constructor for formatter-based serialization is now marked obsolete, matching the runtime's obsolete serialization API. Custom exceptions should use the ordinary message and inner-exception constructors.

Handle absence explicitly with a pattern check or a fallback value. Avoid replacing these checks with null-forgiving operators; a report loaded from historical JSON may legitimately omit the value.

## Package validation and trimming

Every pipeline pack enables SDK package validation for runtime libraries. Validation compares compatible target assets and rejects missing APIs. The clock option is an intentional additive API in the modern build. Compiler extension packages have no runtime reference assemblies to compare.

The new target does not assert trimming or Native AOT compatibility. Runtime discovery of test attributes, retry hooks, context constructors, and exception metadata still uses reflection. Keep that metadata when configuring a trimmed host. The source-generated JSON path alone does not make the execution engine trim-safe.
