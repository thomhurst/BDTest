# BDTest HTML Report Generator

The BDTest framework now includes a built-in HTML report generator that creates beautiful, self-contained HTML reports perfect for publishing to Azure DevOps build summaries or sharing with stakeholders.

## Features

- **Single-page HTML report** - Everything embedded in one file (CSS, JavaScript, data); works offline
- **Run verdict first** - Failed count, duration, run metadata and a status bar at the top
- **Needs attention** - Failed scenarios grouped by shared exception, with links to each scenario
- **Search and filter** - Search scenarios, steps, stories and tags; filter by status or tag; sort by story, failures or duration; group by story
- **Detailed test information** - Steps with keywords and durations, exceptions with copyable stack traces, retry history, scenario outline examples, output and custom HTML
- **Safe custom HTML** - HtmlWriter output renders in a sandboxed frame with scripts disabled
- **Evidence links and images** - Links open in new tabs that retain the sandbox restrictions. Relative screenshots work alongside reports opened from disk; only local reports retain the frame's origin so the browser can load local images. Scripts remain disabled in both the frame and new tabs.
- **Light and dark** - Follows the browser's color scheme; prints cleanly
- **Mobile-friendly** - Responsive layout that works at phone width

## Usage

Every export needs the run that recorded the tests. For NUnit adapter tests, obtain
that run from `NUnitBDTestAssembly` using a type in the test assembly. Standalone
hosts instead pass the same `BDTestRun` they supplied to their builders. Export
all reports before the owner disposes the run.

### Basic Usage

```csharp
using BDTest.Helpers;
using BDTest;

var run = NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var htmlReport = BDTestHtmlHelper.GetTestHtmlReport(null, run);
File.WriteAllText("test-report.html", htmlReport);
```

`MyTests` represents one of your test classes. A null descriptor leaves custom
report metadata unset.

### With Custom Run Descriptor

```csharp
using BDTest;
using BDTest.Helpers;

var run = NUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var runDescriptor = new BDTestRunDescriptor
{
    Environment = "Production",
    Tag = "Release-v1.0",
    BranchName = "main"
};

var htmlReport = BDTestHtmlHelper.GetTestHtmlReport(runDescriptor, run);
File.WriteAllText("test-report.html", htmlReport);
```

### Integration with Test Framework

Place this NUnit `SetUpFixture` outside any namespace in your test assembly so
its teardown runs after all fixtures. If you already have assembly teardown,
combine report exports there and dispose the assembly run only after every export.

```csharp
using BDTest.Helpers;
using BDTest;
using NUnit.Framework;

[SetUpFixture]
public class TestReportSetup
{
    [OneTimeTearDown]
    public void GenerateReports()
    {
        var assembly = typeof(TestReportSetup).Assembly;
        try
        {
            var run = NUnitBDTestAssembly.GetRun(assembly);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            var jsonData = BDTestJsonHelper.GetTestJsonData(null, run);
            File.WriteAllText($"BDTest-Report-{timestamp}.json", jsonData);

            var htmlData = BDTestHtmlHelper.GetTestHtmlReport(null, run);
            File.WriteAllText($"BDTest-Report-{timestamp}.html", htmlData);
        }
        finally
        {
            NUnitBDTestAssembly.Dispose(assembly);
        }
    }
}
```

See the [assembly run guide](docs/docs/configuration/assembly-runs.md) for
configuration and ownership in other test frameworks.

## Azure DevOps Integration

Generate the report after your tests, then publish it as a downloadable artifact in Azure DevOps Services. For example, if your teardown writes `BDTest-Report.html` to `$(Build.ArtifactStagingDirectory)`:

```yaml
- publish: '$(Build.ArtifactStagingDirectory)/BDTest-Report.html'
  artifact: BDTestReport
  condition: succeededOrFailed()
```

Download the file from the run's artifacts and open it in a browser. The `publish` step uploads the file; displaying interactive HTML in a build tab requires a separate HTML viewer extension. See [Publish pipeline artifacts](https://learn.microsoft.com/en-us/azure/devops/pipelines/artifacts/pipeline-artifacts?view=azure-devops).

`PublishTestResults@2` accepts test-result formats such as TRX, NUnit, and JUnit, not this HTML report. Publish those results separately if you also want Azure DevOps test analytics. See [supported test-result formats](https://learn.microsoft.com/en-us/azure/devops/pipelines/tasks/reference/publish-test-results-v2?view=azure-pipelines).

## Report Structure

The HTML report includes:

- **Run result**: Verdict, duration, start and end times (UTC), and environment, tag, branch, machine and version
- **Summary**: Status bar and count tiles include built-but-never-run definitions in the total. Status tiles filter recorded scenarios; the separate **Built but never run** tile links to unfinished definitions. A run with unfinished definitions never reports **All passed**.
- **Needs attention**: Failures grouped by exception type and message (only when something failed)
- **Scenarios**: Searchable, filterable list grouped by story, with expandable details
- **Tests not run**: Tests that were built but never executed (if any)

Each test scenario shows:
- Test status with color-coded indicators
- Scenario and story text
- Execution duration and retry count
- Expandable details including:
  - Step-by-step execution with passed, failed, skipped, inconclusive, not-implemented, not-run, or pending status
  - Exception details with stack traces
  - Console output
  - Custom HTML content

## Benefits

- **Shareable**: Single HTML file can be easily shared via email or web
- **Offline viewing**: No server required, works from file system
- **Professional presentation**: Clean design suitable for stakeholder reviews
- **Comprehensive**: All test information in one place
- **Accessible**: Works in any modern web browser
