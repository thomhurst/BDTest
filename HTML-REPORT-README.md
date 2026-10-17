# BDTest HTML Report Generator

The BDTest framework now includes a built-in HTML report generator that creates beautiful, self-contained HTML reports perfect for publishing to Azure DevOps build summaries or sharing with stakeholders.

## Features

- **Single-page HTML report** - Everything embedded in one file (CSS, JavaScript, data)
- **Professional design** - Clean, modern interface with responsive layout
- **Interactive filtering** - Filter test scenarios by status (Passed, Failed, Skipped, etc.)
- **Detailed test information** - Shows steps, exceptions, output, and custom HTML content
- **Test summary** - Visual summary cards with counts and timing information
- **Mobile-friendly** - Responsive design that works on all devices

## Usage

### Basic Usage

```csharp
using BDTest.Helpers;

// Generate HTML report with default settings
var htmlReport = BDTestHtmlHelper.GetTestHtmlReport();
File.WriteAllText("test-report.html", htmlReport);
```

### With Custom Run Descriptor

```csharp
using BDTest.Helpers;

var runDescriptor = new BDTestRunDescriptor
{
    Environment = "Production",
    Tag = "Release-v1.0", 
    BranchName = "main"
};

var htmlReport = BDTestHtmlHelper.GetTestHtmlReport(runDescriptor);
File.WriteAllText("test-report.html", htmlReport);
```

### Integration with Test Framework

Add to your test setup/teardown (e.g., NUnit SetUpFixture):

```csharp
[SetUpFixture]
public class TestReportSetup
{
    [OneTimeTearDown]
    public void GenerateReports()
    {
        // Generate both JSON and HTML reports
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        
        var jsonData = BDTestJsonHelper.GetTestJsonData();
        File.WriteAllText($"BDTest-Report-{timestamp}.json", jsonData);
        
        var htmlData = BDTestHtmlHelper.GetTestHtmlReport();
        File.WriteAllText($"BDTest-Report-{timestamp}.html", htmlData);
    }
}
```

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

- **Header**: Environment, tag, branch, machine, and version information
- **Summary**: Visual cards showing test counts by status and timing information  
- **Test Scenarios**: Filterable list of all test scenarios with expandable details
- **Not Run Tests**: List of tests that were not executed (if any)

Each test scenario shows:
- Test status with color-coded indicators
- Scenario and story text
- Execution duration and retry count
- Expandable details including:
  - Step-by-step execution with passed, failed, skipped, inconclusive, or not-implemented status
  - Exception details with stack traces
  - Console output
  - Custom HTML content

## Benefits

- **Shareable**: Single HTML file can be easily shared via email or web
- **Offline viewing**: No server required, works from file system
- **Professional presentation**: Clean design suitable for stakeholder reviews
- **Comprehensive**: All test information in one place
- **Accessible**: Works in any modern web browser