---
title: Upgrading to the next major version
description: Start here when migrating existing BDTest tests, adapters and report hosts.
---

# Upgrading to the next major version

This guide covers changes already merged into `main` for the next major release
(3.x). It is not a claim that every roadmap item or package has been released.
Keep all BDTest package versions aligned and use the documentation for your
installed version. The [roadmap](https://github.com/thomhurst/BDTest/issues/860)
tracks work still in progress; update this guide when another breaking change lands.

## Packages and prerequisites

Check the [installation matrix](../getting-started/installation.md#supported-targets)
before upgrading. The core and most adapters now offer `netstandard2.0` and
`net8.0` assets. NUnit requires NUnit 5 and .NET 8 or later; the report host and
file-system storage package require .NET 10. Delegate overload resolution needs
C# 13 or later (.NET SDK 9 or later), even when selecting the .NET Standard asset.
Compiler extensions still target their .NET Standard compiler host.

Package IDs stay the same, including `BDTest.xUnit` and `BDTest.Razor.Reports`.
The latter's display name is **BDTest - ASP.NET Core Reports**.
`BDTest.ReportGenerator.RazorServer` is a sample host, not a NuGet package.
`BDTest.Reports.Storage.FileSystem` is an optional storage package; its addition
does not require replacing an existing custom store.

The [target migration guide](target-frameworks.md) explains internal compiler
polyfills, nullable contracts and modern clock APIs. Nullable annotations may
surface warnings in consumers without changing runtime signatures. Runtime
packages retain their signing key. Starting with 3.x, assembly versions remain
`Major.0.0.0` across minor and patch releases; package and informational versions
still identify the exact release. This does not permit mixing package versions.

## Test code and execution

| Existing usage | Upgrade action |
| --- | --- |
| Nested builder, base or attribute namespaces | Replace test-authoring imports with `using BDTest;`; see [consumer namespaces](consumer-namespaces.md). Rebuild consumers and update reflection names. |
| Expression-tree step signatures | Use [delegate steps](delegate-steps.md). Install `BDTest.SourceGenerator` in projects containing steps to preserve method metadata, attributes and supported argument capture. Use `WithArgs` for unsupported expressions. |
| Positional `[StepText]` placeholders and overrides | Use [parameter names](named-step-text.md): `{amount}` instead of `{0}`, and `"user=an administrator"` instead of `"1:an administrator"`. |
| Synchronous `BDTest()` | Return `Task` from the test and use `await` or `BDTestAsync()`; see [async-only execution](async-only.md). |
| Global execution settings in adapters | `BDTestSettings` has been removed. Move settings into `IBDTestConfiguration` selected by the assembly's `BDTestConfiguration` attribute. Built-in adapters use a frozen [assembly run](../configuration/assembly-runs.md). |
| Reports without an explicit run | Obtain the adapter's assembly run and pass it to report helpers. Export after tests and teardown, then dispose through the assembly owner. Overloads without a run and the legacy default run have been removed. Standalone callers must own an explicit `BDTestRun`; follow [explicit run migration](explicit-runs.md). |
| Old fluent types, `WithContext`, report ID or parameter members | Apply the [public names and fluent builder replacements](fluent-builders.md). Each step kind is sealed; use `var` for continuations. |
| Direct calls to text/version helpers, default constants or JSON converters | Use the [supported helper replacements](internal-helpers.md); implementation helpers are now internal on both core assets. |
| Pending results typed as `List<BuildableTest>` | Use [scenario definition summaries](scenario-definitions.md). Pending results are data snapshots, not executable builders. |
| A custom adapter relying on stack inspection | Supply the runner's exact `MethodInfo` during `BeginTest`; follow [custom adapter migration](../frameworks/custom-adapters.md#method-identity-and-migration). |
| Exact generated text snapshots or transitive Humanizer APIs | Review [generated name changes](generated-name-text.md); add a direct Humanizer reference if your own code needs it. |

Use [context factories and lifetime hooks](../writing-tests/test-context.md) for
async initialization, retry recreation and owned disposal. An inline context
you construct yourself remains yours to dispose. Native adapters now share the
[composition lifecycle](../frameworks/composition.md); remove manual lifecycle
calls when opting into native registration.

## Retry and scenario skip policies

[Retry attributes](../writing-tests/retries.md) now replace run retry rules instead of extending their limits. Precedence is test method, concrete fixture class, assembly, then run defaults. Remove an attribute to opt into run rules; use `[BDTestRetry(0)]` to opt out of all retries. Keep method attributes on the actual adapter-supplied test method, not helper methods.

A retry predicate error now stops retrying and records an output diagnostic while preserving the original test exception. [Scenario skip predicates](../writing-tests/skipping.md#skipping-whole-scenarios) run once before initial context initialization and scenario hooks, not on every attempt. Move skip decisions that need execution-time state into steps using the framework's skip exception. A throwing scenario skip predicate fails immediately without retries. Step skip predicates retain their per-attempt behavior.

## Framework adapters

- [xUnit](../frameworks/xunit.md) now requires xUnit v3. Replace v2 packages,
  use an executable test project, remove constructors that only pass
  `ITestOutputHelper` to the base class, and preserve base async cleanup.
- [NUnit](../frameworks/nunit.md) uses NUnit 5. Composition requires
  `[BDTestFixture]` and `INUnitBDTest`; inheritance remains available.
- [MSTest](../frameworks/mstest.md) requires `[BDTestClass]` on concrete BDTest
  fixtures, including inherited ones. Native context injection and invocation
  completion replace the former setup/cleanup hooks.
- [TUnit](../frameworks/tunit.md) uses global lifecycle hooks. Composition
  implements `IBDTestTUnitTest`; reserve the documented final hook order for
  report export and run disposal.

Use the adapter guide's runner configuration; an adapter's .NET Standard asset
does not establish that the framework or runner supports .NET Framework.

## Reports and hosting

| Area | Upgrade action |
| --- | --- |
| Status handling | Handle `NotRun` and `Pending`; reserve `Inconclusive` for its actual outcome. Store names, not new enum integers. See [status migration](statuses.md). |
| Serialization | Use `BDTestJsonHelper` and the source-generated System.Text.Json metadata instead of Newtonsoft settings or converters. Review the [JSON shape and historical-reader rules](json-serialization.md). |
| Exceptions | Review [structured exception fields](structured-exceptions.md) and update consumers of exception details. |
| Host options | Replace `BDTestReportServerOptions` and middleware configuration with `AddBDTestReports`, `BDTestReportOptions`, and DI registrations. Replace `IAdminAuthorizer` with a named policy. See [options migration](report-options.md). |
| Storage and deletion | Implement cancellable `IReportStore` and `IRunIndexStore`, or use the temporary legacy bridge. Replace the old delete route with `DELETE /bdtest/reports/{id}` and handle 204. See [storage migration](report-storage.md). |
| Authentication and uploads | Configure report-read and admin policies separately from the upload key. Unconfigured reads are denied and blank upload keys disable uploads. See [security](../report-server/security.md). |
| URLs and proxies | Use `MapBDTestReports` and pass the full external report prefix to upload clients. See [hosting](../report-server/hosting.md). |
| Razor view overrides | Update the Overview, Timings, Failures and Not run model types and replace layout scenario access with aggregates. See [compact page model migration](report-storage.md#compact-razor-page-models). |
| Capacity and custom rendering | Check [upload/cache limits](../report-server/caching-and-limits.md) and [standalone HTML security](../reports/html-report-security.md) and the hosted [HTTPS image policy](../report-server/security.md#security-headers). |

The new [BDTest.Contracts](../reports/contracts.md) package is currently additive.
Existing helpers and hosted APIs still use the legacy model; do not send the new
versioned DTO JSON to those endpoints until their integration has shipped.
Likewise, pending roadmap work is not a prerequisite for migrating the changes above.

## Verify the upgrade

Build with nullable diagnostics enabled, run each adapter's tests, and inspect a
passing, failing, skipped and retried scenario in your report. Check that the
export selects the owning run, that historical reports still load, and that
your host's read/upload/delete permissions behave as configured.

For a new project, start with the [compiled first-test walkthrough](../getting-started/first-test.md).
For a SpecFlow conversion, use [Coming from SpecFlow](from-specflow.md).
