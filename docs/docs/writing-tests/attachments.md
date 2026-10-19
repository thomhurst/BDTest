---
title: Attachments
description: Capture screenshots, logs and files with run-owned temporary storage and step association.
---

# Attachments

`Attachments` on the test base and `BDTestContext.Attachments` implement `BDTest.Interfaces.IBDTestAttachments`:

```csharp title="Illustrative fragment"
Attachments.Attach("response.json", responseBytes, "application/json");

using var log = File.OpenRead("test.log");
BDTestContext.Attachments.Attach("test.log", log, "text/plain");

Attachments.AttachFile("network.har", "application/json");
```

Capture copies the content immediately to temporary files owned by the test's run. The stream overload reads from its current position, supports non-seekable streams, and leaves the source open. `AttachFile` uses only the source file's basename as the display name. Its default MIME type is `application/octet-stream`; supply an explicit type for images or text.

Names must contain 1–255 characters without control characters or path separators. MIME types must be concrete, valid types of at most 256 characters without control characters. Capture rejects invalid metadata before copying content, using the same rules as the report server.

## Ownership and hooks

Capture inside a step, `OnBeforeStep`, or `OnAfterStep` adds metadata to that `Step.Attachments` collection. For example:

```csharp title="Illustrative fragment"
public override async Task OnAfterStep(BDTest.Contracts.StepResult step)
{
    if (step.Status == Status.Failed)
    {
        var screenshot = await page.ScreenshotAsync();
        Attachments.Attach("failure.png", screenshot, "image/png");
    }
}
```

Outside a step, capture adds to `Scenario.Attachments`. Setup capture waits for the scenario to be created. If the test ends without creating a scenario, adapter teardown deletes its pending attachments and releases their byte quota. Captures that start or finish after `EndTest` throw `InvalidOperationException`; an in-progress copy rolls back its file and byte quota. Saved writers and context attachment writers retain the lifetime of their original execution, including writers created in a constructor before adapter setup. Capture during an outline's example factory belongs to that example, including when the factory throws. Setup and teardown for the whole outline belong to the outline, including background setup copies that finish while an example is running.

Each attachment has `Id`, `Name`, `MimeType`, `Length`, `AttemptNumber`, and nullable `Uri` immutable `AttachmentInfo` metadata in namespace `BDTest.Contracts`. Collections retain files from every retry; `AttemptNumber` is one-based and corresponds to `Scenario.Attempts.Number`. Files are not copied between attempts or example rows. Content from abandoned step tasks is rejected after the step and its after hooks finish.

Parallel tests and explicit runs keep independent ownership, even when separate runs use the same framework test ID. Custom adapters must supply distinct execution IDs for parallel tests within a run.

## Limits

Configure an [explicit run](../advanced/explicit-runs.md) before starting it:

```csharp title="Illustrative fragment"
var options = new BDTestOptions();
options.Attachments.MaximumAttachmentBytes = 20 * 1024 * 1024;
options.Attachments.MaximumRunBytes = 200 * 1024 * 1024;
using var run = BDTestRun.Start(options);
```

Limits are copied at `Start` and cannot change for that run. Defaults are 10 MiB per attachment and 100 MiB per run, for every run. Zero permits only empty content; negative limits are rejected at `Start`. Concurrent copies share the run limit. A limit violation throws `IOException` and removes the partial file without consuming quota. Source read/write failures also roll back partial files and quota. Names must be nonempty and MIME types must be concrete, valid types.

## Reading and cleanup

`run.OpenAttachment(attachment.Id)` opens captured content for a custom exporter. Dispose the returned stream. An ID from another run, or an ID cleared from this run, throws `KeyNotFoundException`. A disposed run throws `ObjectDisposedException`.

Report JSON includes metadata only. Local paths and file content are never serialized. Historical JSON without attachment collections reads as empty collections. `Uri` is initially null.

Keep the run alive until tests, writers and exporters finish. Use `using` or `try/finally` so disposal removes temporary files even when tests or export fail. `BDTestUtil.ClearScenarios(run)` also removes that run's temporary files and resets its quota. For framework adapters, dispose through the assembly owner after exporting all results. Neither operation touches another run's files.

## Uploading

`BDTestReportServer.SendDataAndGetReportUriAsync` uploads report JSON first, then streams each referenced attachment from that run's temporary files. Explicit-run overloads never read another run's files. Captures from retry attempts and outline examples are included. Pending files without a scenario are not uploaded. Keep the run alive until the whole upload completes; neither success nor failure clears its files automatically.

Configure an [attachment store](../report-server/persistence.md#attachments) on the server and pass a caller-owned `HttpClient` carrying `X-BDTest-Upload-Key`. An older server that does not issue an attachment receipt causes the upload method to throw when the run has attachments. The contracts-based host supports schema-version-1 uploads, including attachment metadata retained across retries, and reads historical reports. Upgrade older hosts before using the current exporter; see [server compatibility](../migration/immutable-results.md).

Pass a final `CancellationToken` to the upload method to cancel an active attachment request or its retry delay. No later attachment starts once cancellation is observed. Cancellation closes upload streams but retains captures until the run is cleared or disposed, and does not undo report or attachment requests already accepted by the server. See [upload cancellation](../report-server/sending-results.md#cancellation).

The uploader uses bounded stream buffers and reopens each captured file on retry. Permanent HTTP failures stop immediately; transient failures follow the existing retry policy. A failure leaves the report and any completed attachments on the server, with missing content returning 404. Retrying the whole method creates a new report, so delete an incomplete report through the normal report deletion flow when needed. Use `try/finally` to dispose the owned run after reporting, including failed uploads.

`GET /bdtest/report/{reportId}/attachments/{attachmentId}` downloads a recorded attachment with its recorded MIME type and a safe filename. All types, including HTML and SVG, use `Content-Disposition: attachment`, `nosniff` and a sandbox CSP. These requests follow the host's report authorization policy. `Uri` remains null; construct the download route from the server-assigned report ID and attachment ID. The server rejects uploader-supplied attachment URIs and local paths.

## Viewing in Razor reports

The scenario page and selected-scenario panel show attachments beneath their owning step or scenario. Before/after-step hook captures stay with that step; setup and teardown captures appear in the scenario attachments section. Outline examples keep their own files. Attachments are grouped by their recorded **Attempt** number, including files from earlier retries even when the final attempt passed. Expand a step on the scenario page to see its files.

PNG, JPEG, GIF, WebP, BMP and AVIF MIME types have inline thumbnails, including valid MIME types with parameters such as `image/png; version=1`. Select a thumbnail to open a larger preview; use **Close preview** or Escape to return. Every file also has a download link. SVG, HTML, PDF, unknown image types and other content are download-only. Names and MIME metadata appear as text; attachment content is never inserted as HTML. A filename extension does not enable previews.

The views build URLs from the server-assigned report ID and recorded attachment ID, respecting the host path base and report route prefix. They use the same authorized download endpoint for images and files, preserving its download disposition, `nosniff` and sandbox headers. Uploaded `Uri` metadata is never used for navigation.

Missing files (for example, after retention or an interrupted upload) return 404 without preventing the report from rendering. A failed image displays a preview-unavailable message and keeps the download link. Historical reports without attachments render as before. These previews are part of the Razor report server; the standalone HTML exporter does not embed captured binary files.
