---
title: Reports overview
description: The ways to get results out of BDTest - console output, a self-contained HTML file, JSON data and the report server.
---


Install `BDTest.Reporting` alongside your adapter to export JSON, generate HTML or upload reports.
Use matching package versions; see [reporting package migration](../migration/reporting-package.md).
# Reports overview

BDTest collects scenarios in their owning run: stories, scenario text, steps, statuses, timings, output, exceptions, tags, retry history and tests that were built but never run. You choose how to publish that data.

| Output | How | Best for |
| --- | --- | --- |
| Console | Automatic | Reading a single test's result in your IDE or CI log. |
| [HTML report](html-report.md) | `BDTestHtmlHelper.GetTestHtmlReport(descriptor, run)` | One self-contained file to attach to a build or email. |
| [JSON data](json-data.md) | `BDTestJsonHelper.GetTestJsonData(descriptor, run)` | Your own tooling, archives, or uploading later. |
| [Report server](../report-server/overview.md) | `BDTestReportUploader.UploadReportAsync(...)` | Sharing links, comparing runs, trends and flakiness. |

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

## Framework context adapters

`BDTest.Reporting` provides `BDTest.Helpers.BDTestReportContextExtensions` for
passing a live framework or hook context to reporting. A typed delegate maps it
to the common `BDTestRunDescriptor`:

```csharp sample="TUnit/ContextReport.cs" title="Compiled example"
using BDTest;
using BDTest.Helpers;
using TUnit.Core;

public static class ContextReport
{
    // Call while this context is valid, after the selected run has completed.
    public static string Create(TestContext context, BDTestRun run)
    {
        return context.CreateHtmlReport(run, Map);
    }

    public static BDTestRunDescriptor Map(TestContext context)
    {
        var descriptor = new BDTestRunDescriptor { Tag = "Acceptance" };
        descriptor.CustomProperties["Framework"] = "TUnit";
        descriptor.CustomProperties["Exporting test ID"] = context.Id;
        return descriptor;
    }
}
```

The adapter is a `Func<TContext, BDTestRunDescriptor>`: use a lambda or a reusable
method. It runs synchronously exactly once per call. Only the selected descriptor
values enter the report; BDTest does not reflect over, serialize or retain the
framework context. This explicit mapping keeps reporting independent of framework
packages and makes the metadata selection visible. There are no implicit
conversions or built-in rules that copy arbitrary context properties.

The same API accepts NUnit's `TestContext.CurrentContext`, MSTest's injected
`TestContext`, TUnit's `TestContext.Current!`, xUnit v3's `TestContext.Current`,
and framework assembly/hook contexts. Choose properties available at that stage
of the runner lifecycle. Examples of per-test metadata are NUnit's `Test.ID`,
MSTest's `TestName`, TUnit's `Id`, and xUnit v3's `Test?.UniqueID` (which can be
absent outside a test). Map optional values only when present. Do not copy
credentials or whole property bags into a report.

For JSON or uploads, obtain a descriptor with the same adapter and pass it to the
existing helpers:

```csharp title="Illustrative fragment"
var descriptor = context.ToBDTestRunDescriptor(ContextReport.Map);
var json = BDTestJsonHelper.GetTestJsonData(descriptor, run);
var result = await uploader.UploadReportAsync(run, descriptor);
```

Conversion happens immediately, so an adapter that copies strings produces metadata
that can be used after the framework context expires. No context is needed for
later exports. Adapter exceptions propagate; a null context or adapter is rejected,
and returning a null descriptor throws `InvalidOperationException`.

The context supplies **run-level metadata**, not test discovery or scenario
selection. `CreateHtmlReport` still requires the explicit `BDTestRun`, includes
all scenarios in that run, and returns HTML without saving a file or disposing
the run. A per-test context does not limit an assembly run to that test. Use
assembly-level teardown for a complete assembly report; a per-test completion
hook is appropriate only when you own a separate run for that test. Call after
BDTest's completion hook, as shown in the [TUnit example](../frameworks/tunit.md#composition-with-an-existing-base-class).
For hooks without a live per-test context, map the hook's own context or use a
previously created descriptor with the existing report helpers. Do not mutate a
shared descriptor concurrently with export. Custom properties use the same JSON,
upload and HTML-encoding behavior as other descriptor metadata.

## Console output

While tests run, BDTest writes each scenario's story, scenario text, steps with their status, and any exception to the console. Most test runners show this as the test's output.
