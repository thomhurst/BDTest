---
title: HTML report
description: Generate a single self-contained HTML report with BDTestHtmlHelper.
---

# HTML report

`BDTestHtmlHelper` builds one self-contained HTML page. CSS, JavaScript and data are all embedded, so the file works offline and can be shared by email or attached to a build.

```csharp
using BDTest;
using BDTest.Helpers;

var html = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor
{
    Environment = "Production",
    Tag = "Release-v1.0",
    BranchName = "main",
});

File.WriteAllText("BDTest-Report.html", html);
```

| Overload | Use |
| --- | --- |
| `GetTestHtmlReport()` | Default descriptor. |
| `GetTestHtmlReport(BDTestRunDescriptor)` | Label the run. |
| `GetTestHtmlReport(BDTestRunDescriptor, BDTestRun)` | Report on an [explicit run](../advanced/explicit-runs.md). |

The method returns a string; you decide where to write it.

## Generating it after all tests

TUnit example:

```csharp
public class TestReportSetup
{
    [After(HookType.Assembly)]
    public static async Task GenerateReports()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        await File.WriteAllTextAsync($"BDTest-Report-{timestamp}.json", BDTestJsonHelper.GetTestJsonData());
        await File.WriteAllTextAsync($"BDTest-Report-{timestamp}.html", BDTestHtmlHelper.GetTestHtmlReport());
    }
}
```

For other frameworks, see [Test frameworks](../frameworks/overview.md).

## What the report contains

- **Header:** environment, tag, branch, machine and BDTest version.
- **Summary:** cards with counts by status and timing information.
- **Scenarios:** a filterable list of scenarios (Passed, Failed, Skipped and so on). Each scenario shows its status, scenario and story text, duration and retry count, and expands to show:
  - each step with its status (passed, failed, skipped, inconclusive, not implemented, not run or pending),
  - exception details with stack traces,
  - console output,
  - retry history,
  - [custom HTML](../writing-tests/output.md#custom-html-in-reports).
- **Not run tests:** chains that were built but never executed.

The layout is responsive and works on mobile.

:::warning Custom HTML is not sandboxed here
The standalone HTML report embeds `ScenarioHtmlWriter.Custom(...)` output directly. The report server isolates it in a sandboxed iframe. Only add HTML you trust.
:::

To publish the file from CI, see [CI integration](ci-integration.md).
