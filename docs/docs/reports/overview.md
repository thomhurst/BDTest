---
title: Reports overview
description: The ways to get results out of BDTest - console output, a self-contained HTML file, JSON data and the report server.
---

# Reports overview

BDTest collects scenarios in their owning run: stories, scenario text, steps, statuses, timings, output, exceptions, tags, retry history and tests that were built but never run. You choose how to publish that data.

| Output | How | Best for |
| --- | --- | --- |
| Console | Automatic | Reading a single test's result in your IDE or CI log. |
| [HTML report](html-report.md) | `BDTestHtmlHelper.GetTestHtmlReport(descriptor, run)` | One self-contained file to attach to a build or email. |
| [JSON data](json-data.md) | `BDTestJsonHelper.GetTestJsonData(descriptor, run)` | Your own tooling, archives, or uploading later. |
| [Report server](../report-server/overview.md) | `BDTestReportUploader.UploadAsync(...)` | Sharing links, comparing runs, trends and flakiness. |

Pass the [adapter assembly run](../configuration/assembly-runs.md) to each helper, and call them **after all tests have finished**, typically in an assembly-level teardown. See the "Reports after the run" section of each [framework page](../frameworks/overview.md).

## Run descriptors

A `BDTestRunDescriptor` labels a whole test run. Every report helper accepts one:

```csharp title="Illustrative fragment"
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

Reports also record the machine name and BDTest version. Metadata comes only from the supplied descriptor. Missing metadata stays `null` in JSON and uploads; HTML displays "Not Specified". An explicitly empty descriptor value stays empty.

JSON, HTML and uploads use the same report model for the selected run, including its report ID, scenarios, unfinished definitions and timer. Every export requires a run; see [explicit runs](../advanced/explicit-runs.md). Nonempty reports use the earliest scenario start and latest scenario end. Empty reports use the selected run's start time and the export time as their end time.

Set `descriptor.CustomProperties["Build"] = buildNumber` to add report properties. Every export copies that descriptor's dictionary into its report model. JSON and uploads include `CustomProperties`; HTML shows them under **Run properties**. Do not mutate a descriptor concurrently with export. Different runs never inherit another descriptor's metadata.

## Console output

While tests run, BDTest writes each scenario's story, scenario text, steps with their status, and any exception to the console. Most test runners show this as the test's output.
