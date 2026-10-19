---
title: Shared HTML renderer
description: Render immutable reports without test execution and share encoded fragments with hosted reports.
---

# Shared HTML renderer

`BDTest.Rendering` is a signed `netstandard2.0` package that depends only on
`BDTest.Contracts`. Both `BDTest.Reporting` and `BDTest.Razor.Reports` reference
it transitively. A tool that already has immutable report data can reference it
directly without loading execution, a test adapter, or ASP.NET Core:

```bash
dotnet add package BDTest.Rendering
```

```csharp title="Illustrative fragment"
using BDTest.Contracts;
using BDTest.Rendering;

BDTestReport report = BDTestReportJson.Deserialize(json);
string html = ReportHtmlRenderer.GenerateHtmlReport(report);
File.WriteAllText("report.html", html);
```

Use matching versions of all BDTest packages. `BDTestReportJson` reads the current
schema. Normalize unversioned historical input through the
[historical reader](json-data.md#reading-and-writing-legacy-reports) or a hosted
`HostedReportJson` reader before rendering. The renderer accepts immutable
contracts and never deserializes execution objects.

## Rendering decision

The shared implementation emits HTML on the server with the existing embedded
CSS and event-listener script. This preserves the standalone single-file offline
report without requiring a JavaScript framework, browser build, or runtime
network requests. It uses the contracts package's source-generated JSON metadata
for tag data. The [parent CSP and custom HTML sandbox](html-report-security.md)
remain unchanged.

`ReportHtmlRenderer.RenderArguments` renders encoded tables and doc strings,
including the existing twenty-row table preview. `RenderException` renders
encoded exception trees, data, inner exceptions, truncation notices and original
diagnostic text. Hosted scenario, example and retry views use these same
implementations. Standalone exception text retains its copy and wrap controls.

The renderer does not open attachment URIs, authorize requests, select a run,
materialize hosted cache entries, or upload reports. Those responsibilities stay
with their current owners. Hosted attachment URLs continue to be generated from
the stored report identity by the authorized attachment route.

## Migration and integration boundary

Existing `BDTest.Helpers.BDTestHtmlHelper`, `ExceptionHtmlHelper` and
`StepArgumentHtmlHelper` signatures remain in `BDTest.Reporting` as forwarding
facades. Run-based exports still use its single model builder and validate the
selected run. No caller migration is needed beyond the
[reporting package reference](../migration/reporting-package.md); tools wanting
only rendering can use `ReportHtmlRenderer` directly.

This foundation shares standalone rendering and hosted argument/exception
fragments. Hosted page composition still uses Razor views; full hosted/standalone
section integration and page-level parity are tracked by
[issue #993](https://github.com/thomhurst/BDTest/issues/993). Compact cached page
projections, customization, navigation, historical reading and attachment
security remain in the hosted package during that integration.
