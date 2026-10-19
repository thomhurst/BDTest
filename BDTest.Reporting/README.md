# BDTest.Reporting

Generate JSON, self-contained HTML and HTTP report uploads from a BDTest run.
Install this package alongside your BDTest adapter, using matching versions.
Both `netstandard2.0` and `net8.0` assets are included.

```csharp
using BDTest;
using BDTest.Helpers;

// Export the run owned by your adapter after all tests have completed.
var html = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), run);
File.WriteAllText("report.html", html);
```

The next major release moves these APIs from the core assembly while retaining
namespaces and method signatures. Add the package reference and rebuild callers.
Core does not reference reporting. Run snapshots and attachment capture remain
in core; this package owns export metadata, serialization and upload. HTML
helpers delegate to the transitive BDTest.Rendering package, which depends only
on immutable contracts and can be used directly by tools without execution.

See https://thomhurst.github.io/BDTest/docs/reports/overview for run lifecycle,
JSON compatibility and report-server upload configuration.
