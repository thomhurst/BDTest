---
title: Reports overview
description: The ways to get results out of BDTest - console output, a self-contained HTML file, JSON data and the report server.
---

# Reports overview

BDTest collects every scenario that runs in the test process: stories, scenario text, steps, statuses, timings, output, exceptions, tags, retry history and tests that were built but never run. You choose how to publish that data.

| Output | How | Best for |
| --- | --- | --- |
| Console | Automatic | Reading a single test's result in your IDE or CI log. |
| [HTML report](html-report.md) | `BDTestHtmlHelper.GetTestHtmlReport()` | One self-contained file to attach to a build or email. |
| [JSON data](json-data.md) | `BDTestJsonHelper.GetTestJsonData()` | Your own tooling, archives, or uploading later. |
| [Report server](../report-server/overview.md) | `BDTestReportServer.SendDataAndGetReportUriAsync(...)` | Sharing links, comparing runs, trends and flakiness. |

All three helpers read from the same in-process store, so call them **after all tests have finished**, typically in an assembly-level teardown. See the "Reports after the run" section of each [framework page](../frameworks/overview.md).

## Run descriptors

A `BDTestRunDescriptor` labels a whole test run. Every report helper accepts one:

```csharp
var descriptor = new BDTestRunDescriptor
{
    Environment = "Staging",
    Tag = "API acceptance tests",
    BranchName = Environment.GetEnvironmentVariable("GITHUB_REF_NAME"),
};
```

| Property | Shown as |
| --- | --- |
| `Environment` | The environment the tests ran against. |
| `Tag` | A label for this kind of run, for example the suite name. Not the same as [scenario tags](../writing-tests/tags-and-information.md). |
| `BranchName` | The source branch under test. |

Reports also record the machine name and BDTest version. Descriptor values take precedence over the obsolete `BDTestSettings.Environment`, `Tag` and `BranchName` fallbacks. Missing metadata stays `null` in JSON and uploads; HTML displays "Not Specified". An explicitly empty descriptor value stays empty.

JSON, HTML and uploads use the same report model for the selected run, including its report ID, scenarios, unfinished definitions and timer. Existing overloads select the default run; see [explicit runs](../advanced/explicit-runs.md) for isolated reports. Nonempty reports use the earliest scenario start and latest scenario end. Empty reports use the selected run's start time and the export time as their end time.

While the obsolete `BDTestSettings.CustomProperties` setting exists, every output copies its values at export time, including reports for explicit runs. JSON and uploads include `CustomProperties`; HTML shows them under **Run properties**. Configure the dictionary before exporting, without concurrent changes. These legacy report settings remain process-wide even when execution uses explicit run options.

## Console output

While tests run, BDTest writes each scenario's story, scenario text, steps with their status, and any exception to the console. Most test runners show this as the test's output.
