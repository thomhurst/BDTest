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
4. The server creates a new ID for the report and returns its URL.

The server ignores any client-supplied report ID. Each accepted upload, including a retry after a lost response, creates a separate report.

## Server-side limits

- `MaxCompressedUploadBytes` limits request body bytes before decompression; `MaxUploadBytes` limits decompressed JSON and its UTF-8 representation. Both default to 10 MiB, including chunked requests. Exceeding either byte limit returns HTTP 413.
- `POST /bdtest/data` accepts plain JSON or a single `Content-Encoding: gzip`. The existing uploader sends plain JSON; a custom uploader can gzip its body while keeping a JSON content type. Unsupported encodings return 415; malformed gzip or JSON returns 400.
- Content type must be `application/json`, `text/json` or `application/*+json`, in UTF-8 or UTF-16.
- The rate limit is `UploadsPerMinute` (default 60) authenticated uploads per minute per server instance. Excess requests get HTTP 429 with `Retry-After`.
- Invalid JSON returns HTTP 400.
- In cache-only hosting, a report larger than the cache budget returns HTTP 413. See [Caching and limits](caching-and-limits.md).

After the upload, open the report server. The newest run is at the top of the test run list.
