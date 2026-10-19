---
title: HTML report
description: Generate a single self-contained HTML report with BDTestHtmlHelper.
---


Install `BDTest.Reporting` alongside your adapter to export JSON, generate HTML or upload reports.
Use matching package versions; see [reporting package migration](../migration/reporting-package.md).
# HTML report

For built-in adapters, pass the [configured assembly run](../configuration/assembly-runs.md) to report/utility helpers. Every export and store utility requires the owning run. Export all reports before disposing through the assembly owner.


`BDTestHtmlHelper` builds one self-contained HTML page. CSS, JavaScript and data are all embedded, so the file works offline and can be shared by email or attached to a build.

```csharp title="Illustrative fragment"
using BDTest;
using BDTest.Helpers;

var run = BDTest.NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var html = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor
{
    Environment = "Production",
    Tag = "Release-v1.0",
    BranchName = "main",
}, run);

File.WriteAllText("BDTest-Report.html", html);
```

| Overload | Use |
| --- | --- |
| `GetTestHtmlReport(BDTestRunDescriptor, BDTestRun)` | Report on an [explicit run](../advanced/explicit-runs.md). |
| `GenerateHtmlReport(BDTest.Contracts.BDTestReport)` | Render an immutable report, including a deserialized schema-versioned export. |

Both methods return a string; you decide where to write it. JSON export and HTML rendering use the same immutable contract snapshots. Rendering a saved report does not require an active run. Timestamps with a non-zero offset in saved reports are converted to UTC for display; the original contract values are preserved.

## Generating it after all tests

TUnit example:

```csharp title="Illustrative fragment"
public class TestReportSetup
{
    [After(HookType.Assembly)]
    public static async Task GenerateReports()
    {
        var assembly = typeof(TestReportSetup).Assembly;
        try
        {
            var run = BDTest.TUnitBDTestAssembly.GetRun(assembly);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            await File.WriteAllTextAsync($"BDTest-Report-{timestamp}.json", BDTestJsonHelper.GetTestJsonData(null, run));
            await File.WriteAllTextAsync($"BDTest-Report-{timestamp}.html", BDTestHtmlHelper.GetTestHtmlReport(null, run));
        }
        finally { BDTest.TUnitBDTestAssembly.Dispose(assembly); }
    }
}
```

For other frameworks, see [Test frameworks](../frameworks/overview.md).

## What the report contains

- **Run result:** the verdict, duration, start and end times (UTC), and the environment, tag, branch, machine and BDTest version.
- **Run properties:** values copied from `BDTestRunDescriptor.CustomProperties`, with keys and values HTML-encoded.
- **Summary:** a status bar and count tiles. Totals include tests that were built but never run, so a run with unfinished definitions never reports **All passed**. Status tiles filter the scenario list; the **Built but never run** tile links to the unfinished definitions.
- **Needs attention:** failed scenarios grouped by exception type and message, with links to each scenario. Shown only when something failed.
- **Scenarios:** a list grouped by story. Search scenarios, steps, stories and tags; filter by status or tag; sort by story, failures first or duration. Large lists show 200 scenarios at a time. Each scenario expands to show:
  - each step with its keyword, duration and status (passed, failed, skipped, inconclusive, not implemented, not run or pending),
  - exception details with stack traces you can wrap and copy,
  - retry history and scenario outline examples,
  - startup, step and teardown output,
  - [custom HTML](../writing-tests/output.md#custom-html-in-reports).
- **Tests not run:** chains that were built but never executed.

The report follows the browser's light or dark colour scheme, prints cleanly and works at phone width. If no scenarios were recorded, it explains when to generate the report.

Custom HTML from `ScenarioHtmlWriter` renders in a sandboxed frame with scripts disabled, and the report has a strict Content Security Policy; see [HTML report security](html-report-security.md). Links open in new tabs that keep the sandbox restrictions. Relative screenshot paths work when the report is opened from disk next to the images.

To publish the file from CI, see [CI integration](ci-integration.md).
