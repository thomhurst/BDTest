# BDTest.Rendering

Render immutable BDTest reports without referencing execution or ASP.NET Core.
This signed package targets netstandard2.0 and depends only on BDTest.Contracts.
Use matching versions of all BDTest packages.

```csharp
using BDTest.Contracts;
using BDTest.Rendering;

BDTestReport report = BDTestReportJson.Deserialize(json);
string html = ReportHtmlRenderer.GenerateHtmlReport(report);
File.WriteAllText("report.html", html);
```

HTML is server-generated with embedded styles and JavaScript: one offline file,
a hash-based parent CSP and sandboxed custom HTML. RenderArguments and
RenderException provide encoded fragments also used by hosted Razor reports.
The package neither reads historical JSON nor opens attachment URIs. Use a
schema-aware reader to normalize historical input before rendering.

BDTest.Reporting references this package for run-based export and keeps its
existing public helpers as forwarding facades. BDTest.Razor.Reports references
it for argument and exception fragments; hosted page composition, compact
cached projections, customization, navigation and authorized attachment delivery
remain in the hosted package. Complete hosted/standalone page integration is
tracked separately in issue #993.

See https://thomhurst.github.io/BDTest/docs/reports/rendering for the package
boundary and migration guidance.
