---
title: Sending results
description: Upload test results to a BDTest report server at the end of a test run.
---

# Sending results

At the end of the test run, call `BDTestReportServer.SendDataAndGetReportUriAsync`. It uploads every scenario collected in the process and returns the URL of the new report.

Only scenarios run through BDTest (`Given(...).When(...).Then(...)`) are sent.

## Example with TUnit

```csharp
using BDTest;

public class SendResultsToReportServer
{
    [After(HookType.Assembly)]
    public static async Task AfterAllTests()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add(
            "X-BDTest-Upload-Key",
            Environment.GetEnvironmentVariable("BDTEST_REPORT_UPLOAD_API_KEY"));

        var reportUri = await BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri("https://reports.example.com"),
            new BDTestRunDescriptor
            {
                Tag = "API acceptance tests",
                BranchName = Environment.GetEnvironmentVariable("BRANCH_NAME"),
                Environment = "Pre-production",
            },
            client);

        Console.WriteLine($"BDTest report: {reportUri}");
    }
}
```

For other frameworks, put the same code in the framework's after-all hook. See [Test frameworks](../frameworks/overview.md).

## The upload key

The report server requires the `X-BDTest-Upload-Key` header on every upload. `SendDataAndGetReportUriAsync` does **not** add the header itself, so pass an `HttpClient` with the header set, as above.

- Keep the key in a CI secret and pass it as an environment variable.
- Use HTTPS.
- A missing, wrong or duplicated key returns HTTP 401. So does a server with no key configured.

## Overloads

| Overload | Use |
| --- | --- |
| `(Uri serverAddress, BDTestRunDescriptor descriptor, HttpClient httpClient)` | Recommended. Your client carries the upload key. |
| `(Uri serverAddress, BDTestRunDescriptor descriptor)` | Creates its own client with a 2-minute timeout. Works only if no proxy or key is needed, so not with current servers. |
| `(BDTestRun run, Uri serverAddress, BDTestRunDescriptor descriptor)` | Upload an [explicit run](../advanced/explicit-runs.md). |
| `(Uri serverAddress, BDTestRunDescriptor descriptor, HttpClient httpClient, BDTestRun run)` | Explicit run with your own client. |
| `(Uri serverAddress)` | Obsolete. |

All return `Task<Uri>`, the summary page of the new report.

## What happens during an upload

1. A best-effort `GET /bdtest/ping` warms up the server. Any path in the server address is replaced, so both requests go to the host root; see [path prefixes](hosting.md#path-prefixes).
2. The report is posted as JSON to `POST /bdtest/data`.
3. On HTTP 408, 429, 500, 502, 503 or 504, a network error or a timeout, the client retries, up to three attempts in total. It honours a `Retry-After` header given in seconds or as an HTTP date.
4. The server creates a new ID for the report and returns its URL. `X-BDTest-Report-Id` identifies it.
5. For reports with attachments, the response also contains `X-BDTest-Attachment-Token`. The client sends that receipt and the upload key to `POST /bdtest/data/{reportId}/attachments/{attachmentId}`, once for each referenced attachment. Files stream from the selected run; each transient retry opens a fresh stream. The method returns only after every attachment succeeds.

The server ignores any client-supplied report ID. Each accepted upload, including a retry after a lost response, creates a separate report.

## Server-side limits

- Requests are limited to 10 MiB, including chunked requests.
- Content type must be `application/json`, `text/json` or `application/*+json`, in UTF-8 or UTF-16.
- The rate limit is `UploadsPerMinute` (default 60) authenticated uploads per minute per server instance. Excess requests get HTTP 429 with `Retry-After`.
- Invalid JSON returns HTTP 400.
- In cache-only hosting, a report larger than the cache budget returns HTTP 413. See [Caching and limits](caching-and-limits.md).

After the upload, open the report server. The newest run is at the top of the test run list.

## Attachment protocol and limits

Configure an [attachment store](persistence.md#attachments) before uploading captures. Report JSON records the complete manifest before any attachment bytes are accepted. Each ID must be unique, each attempt number positive, and names must contain 1–255 characters without control characters or path separators. MIME types must be concrete, valid types of at most 256 characters, without control characters. Uploaded attachment `Uri` values must be null. The server validates per-file bytes, total report bytes and attachment count against `MaximumAttachmentBytes` (10 MiB), `MaximumReportAttachmentBytes` (100 MiB) and `MaximumAttachmentsPerReport` (1000). These limits accept zero and reject negative configuration values. The JSON request retains its separate 10 MiB limit.

Attachment requests require the same upload key and share the upload rate limit. Their body must match the manifest length exactly, including chunked requests. The server stages content on disk with bounded memory before passing it to storage. Size and metadata failures do not publish partial content. Attachment bodies do not need JSON encoding, multipart wrapping, base64 or local paths; metadata comes only from the stored report.

Receiving an attachment body does not block other uploads or report deletion. After staging, the server rechecks the report and receipt under a per-report gate before writing. Deleting or expiring the report during staging makes the upload return 404. Store writes coordinate with deletion for that report while other reports can continue independently.

Upload limits apply to admission of new reports and attachment content. Lowering these limits does not hide previously stored downloads; downloads still validate identifiers and metadata. Both upload headers are required independently of interactive login cookies, so machine uploads do not use browser antiforgery tokens.

Treat the receipt token as a secret and never log it. The server overwrites any supplied `AttachmentUploadTokenHash` with a SHA-256 hash of a new random token; only that hash is persisted in report JSON. Reading a report does not grant write access. A receipt remains valid after a server restart when the report is persisted. A missing or incorrect token returns 401; an unknown report, attachment or missing content returns 404. A receipt cannot write another report, even when attachment IDs match. Report and attachment uploads are separate operations: failure can leave an incomplete report, and retrying the entire upload creates another report. See [attachment lifetime and cleanup](../writing-tests/attachments.md#uploading).
