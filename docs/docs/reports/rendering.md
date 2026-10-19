---
title: Shared HTML renderer
description: Render immutable reports without test execution and share run and scenario sections with hosted reports.
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
remain in place. The hosted shell loads the same scoped styles and interaction
script, so disclosure, copy and wrap controls work in both report surfaces.

`ReportHtmlRenderer.RenderRunOverview` renders the verdict, timing, run metadata,
status totals and custom properties from a `ReportRunOverview`. `FromReport`
builds this small immutable projection from a report. Hosted summary pages supply
the same fields from their compact cached projections, without reconstructing a
report or retaining steps, examples, exceptions or output. A status URL callback
turns offline filter controls into hosted navigation; a separate URL points to
tests that were declared but never ran. `RenderNotRun` supplies the same encoded
warning rows to the offline report and hosted warnings page. Hosted summaries
preserve timing inferred from scenario timestamps when the report has no run timer.

Scenario pages and offline reports share the complete scenario detail renderer:
steps and timings, structured arguments, exceptions, examples, retries, startup
and teardown output, custom information, attachments and sandboxed custom HTML.
Errors recorded only on an outline, such as cancellation between examples,
remain visible; exceptions already shown in an example are not repeated at the
outline level. Hosted routing and standalone filtering wrap the shared sections. Tables retain
the twenty-row preview, and captured output retains copy and wrap controls. `RenderArguments` and `RenderException` also remain available as smaller
encoded fragments.

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

Hosted Razor views supply their navigation and history panels around the shared
content. `RenderAttention` shares failure-group cards over compact
`ReportFailureOverview` and `ReportFailureLink` display data. The host retains
its grouping analysis, three-group/five-scenario overview limits and links to
additional failures; standalone reports retain their root-cause grouping and
in-page scenario navigation. Compact page caches, customization and historical reading
remain in the hosted package. Attachment URL callbacks use the stored report
identity and the authorized hosted attachment route. Without a route resolver,
attachments show their metadata and unavailable-content label. Captured attachment
URIs never become links or previews. URLs are encoded; attachment links must be
application-relative routes and executable URI schemes are rejected. Neither surface lets custom HTML escape its sandbox or bypass
hosted attachment authorization.

For custom host integration, `ReportRenderOptions.AttachmentUrl` resolves each
attachment to an authorized application-relative route, or returns `null` when
content is unavailable. Set `Hosted = true` for the stricter hosted custom-HTML
sandbox. Side panels can set `OmitCustomHtml = true` while offering a link to the
full scenario page. Use `RenderScenario`, `RenderExamples`, `RenderRetryHistory`
and `RenderAttachments` for the corresponding sections. `RenderStyles` returns
the shared style element; `GetScript` returns script text for a host-owned script
element with the host's CSP nonce. Containers use the `rpt` class so styles and
interaction handlers stay scoped to the report content.
