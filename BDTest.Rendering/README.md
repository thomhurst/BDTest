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
a hash-based parent CSP and sandboxed custom HTML. Standalone and hosted Razor
reports share run overviews and complete scenario sections, including steps,
examples, retry attempts, exceptions, output and attachments. RenderArguments and
RenderException remain available for smaller encoded fragments.
The package neither reads historical JSON nor opens attachment URIs. Use a
schema-aware reader to normalize historical input before rendering.

BDTest.Reporting references this package for run-based export and keeps its
existing public helpers as forwarding facades. BDTest.Razor.Reports references
it for the same run and scenario rendering. A compact ReportRunOverview supplies
counts, metadata and timing without retaining a scenario graph or reloading a
cached report. Hosted shells retain history, navigation, customization and compact
page caches; URL callbacks use the host’s authorized attachment route and stored
report identity. The rendering package never performs attachment I/O.

See https://thomhurst.github.io/BDTest/docs/reports/rendering for the package
boundary and migration guidance.
