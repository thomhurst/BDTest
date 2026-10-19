---
title: CI integration
description: Publish BDTest HTML reports from GitHub Actions and Azure DevOps, and upload results to a report server.
---

# CI integration

For built-in adapters, pass the [configured assembly run](../configuration/assembly-runs.md) to report/utility helpers. Every export and store utility requires the owning run. Export all reports before disposing through the assembly owner.


Write the [HTML report](html-report.md) to a known folder in your assembly-level teardown, then publish that folder as a build artifact. To make results available as a link instead, upload them to a [report server](../report-server/sending-results.md).

Use `succeededOrFailed()` / `if: always()` so the report is published when tests fail. That is when you need it most.

## GitHub Actions

```yaml
- name: Test
  run: dotnet run --project MyApp.AcceptanceTests --configuration Release
  env:
    BDTEST_REPORT_DIR: ${{ github.workspace }}/bdtest-report

- name: Upload BDTest report
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: bdtest-report
    path: bdtest-report/
```

```csharp title="Illustrative fragment"
var run = BDTest.TUnit.TUnitBDTestAssembly.GetRun(typeof(MyTests).Assembly);
var directory = Environment.GetEnvironmentVariable("BDTEST_REPORT_DIR") ?? AppContext.BaseDirectory;
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "BDTest-Report.html"), BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor
{
    BranchName = Environment.GetEnvironmentVariable("GITHUB_REF_NAME"),
    Environment = "CI",
}, run));
```

## Azure DevOps

If your teardown writes `BDTest-Report.html` to `$(Build.ArtifactStagingDirectory)`:

```yaml
- publish: '$(Build.ArtifactStagingDirectory)/BDTest-Report.html'
  artifact: BDTestReport
  condition: succeededOrFailed()
```

Download the file from the run's artifacts and open it in a browser. The `publish` step only uploads the file; showing interactive HTML in a build tab needs a separate HTML viewer extension. See [Publish pipeline artifacts](https://learn.microsoft.com/en-us/azure/devops/pipelines/artifacts/pipeline-artifacts?view=azure-devops).

`PublishTestResults@2` accepts test result formats such as TRX, NUnit and JUnit, not this HTML report. Publish those separately if you also want Azure DevOps test analytics. See [supported test result formats](https://learn.microsoft.com/en-us/azure/devops/pipelines/tasks/reference/publish-test-results-v2?view=azure-pipelines).

## Uploading to a report server from CI

Store the upload key as a pipeline secret and pass it to the test process as an environment variable:

```yaml
- name: Test
  run: dotnet run --project MyApp.AcceptanceTests --configuration Release
  env:
    BDTEST_REPORT_SERVER: https://reports.example.com
    BDTEST_REPORT_UPLOAD_API_KEY: ${{ secrets.BDTEST_REPORT_UPLOAD_API_KEY }}
```

Then follow [Sending results](../report-server/sending-results.md) and print the returned URL so it appears in the build log.
