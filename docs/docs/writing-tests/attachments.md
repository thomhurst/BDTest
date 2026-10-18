---
title: Attachments
description: Capture screenshots, logs and files with run-owned temporary storage and step association.
---

# Attachments

`Attachments` on the test base and `BDTestContext.Attachments` implement `BDTest.Interfaces.IBDTestAttachments`:

```csharp
Attachments.Attach("response.json", responseBytes, "application/json");

using var log = File.OpenRead("test.log");
BDTestContext.Attachments.Attach("test.log", log, "text/plain");

Attachments.AttachFile("network.har", "application/json");
```

Capture copies the content immediately to temporary files owned by the test's run. The stream overload reads from its current position, supports non-seekable streams, and leaves the source open. `AttachFile` uses only the source file's basename as the display name. Its default MIME type is `application/octet-stream`; supply an explicit type for images or text.

## Ownership and hooks

Capture inside a step, `OnBeforeStep`, or `OnAfterStep` adds metadata to that `Step.Attachments` collection. For example:

```csharp
public override async Task OnAfterStep(Step step)
{
    if (step.Status == Status.Failed)
    {
        var screenshot = await page.ScreenshotAsync();
        Attachments.Attach("failure.png", screenshot, "image/png");
    }
}
```

Outside a step, capture adds to `Scenario.Attachments`. Setup capture waits for the scenario to be created. If the test ends without creating a scenario, adapter teardown deletes its pending attachments and releases their byte quota. Captures that start or finish after `EndTest` throw `InvalidOperationException`; an in-progress copy rolls back its file and byte quota. Saved writers and context attachment writers retain the lifetime of their original execution, including writers created in a constructor before adapter setup. Capture during an outline's example factory belongs to that example, including when the factory throws. Setup and teardown for the whole outline belong to the outline, including background setup copies that finish while an example is running.

Each attachment has `Id`, `Name`, `MimeType`, `Length`, `AttemptNumber`, and nullable `Uri` metadata in namespace `BDTest.Test`. Collections retain files from every retry; `AttemptNumber` is one-based and corresponds to `Scenario.Attempts.Number`. Files are not copied between attempts or example rows. Content from abandoned step tasks is rejected after the step and its after hooks finish.

Parallel tests and explicit runs keep independent ownership, even when separate runs use the same framework test ID. Custom adapters must supply distinct execution IDs for parallel tests within a run.

## Limits

Configure an [explicit run](../advanced/explicit-runs.md) before starting it:

```csharp
var options = new BDTestOptions();
options.Attachments.MaximumAttachmentBytes = 20 * 1024 * 1024;
options.Attachments.MaximumRunBytes = 200 * 1024 * 1024;
using var run = BDTestRun.Start(options);
```

Limits are copied at `Start` and cannot change for that run. Defaults are 10 MiB per attachment and 100 MiB per run, including for the shared default run. Zero permits only empty content; negative limits are rejected at `Start`. Concurrent copies share the run limit. A limit violation throws `IOException` and removes the partial file without consuming quota. Source read/write failures also roll back partial files and quota. Names must be nonempty and MIME types must be concrete, valid types.

## Reading and cleanup

`run.OpenAttachment(attachment.Id)` opens captured content for a custom exporter. Dispose the returned stream. An ID from another run, or an ID cleared from this run, throws `KeyNotFoundException`. A disposed run throws `ObjectDisposedException`.

Report JSON includes metadata only. Local paths and file content are never serialized. Historical JSON without attachment collections reads as empty collections. `Uri` is initially null.

Keep the run alive until tests, writers and exporters finish. Use `using` or `try/finally` so disposal removes temporary files even when tests or export fail. `BDTestUtil.ClearScenarios(run)` also removes that run's temporary files and resets its quota. For framework adapters using the shared default run, call `BDTestUtil.ClearScenarios()` after exporting all results. Neither operation touches another run's files.

:::note Current scope
This API captures files and exports metadata. Automatic report-server uploads, attachment stores, and Razor previews/downloads are tracked separately in [#890](https://github.com/thomhurst/BDTest/issues/890) and [#891](https://github.com/thomhurst/BDTest/issues/891). Existing report upload sends JSON only; export the content yourself with `OpenAttachment` until those features ship.
:::
