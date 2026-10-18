---
title: Caching and limits
description: The report server's bounded full-report cache, retention, upload size limits and bulk comparison limits.
---

# Caching and limits

## Full-report cache

The report server keeps recently used full reports in a dedicated bounded cache, separate from the host's `IMemoryCache`. Configure it at startup, before the server handles requests:

```csharp
app.UseBDTestReportServer(options =>
{
    options.ReportCacheSizeLimitBytes = 64L * 1024 * 1024; // Default: 64 MiB
    options.ReportCacheRetention = TimeSpan.FromHours(8);   // Default: eight hours
    options.ReportStore = reportStore;
    options.RunIndexStore = runIndexStore;                    // Optional; omit for cache-only hosting
});
```

Both values must be positive; otherwise the setter throws `ArgumentOutOfRangeException`.

### How size is counted

- Each cached report is charged its serialized UTF-8 JSON size. The size is measured by streaming, without keeping a full serialized copy.
- The total of those sizes never exceeds the budget.
- This is an estimate of retained report size, not a limit on process memory. Object overhead, temporary allocations, in-flight responses and run summaries are outside the budget.
- Treat uploaded and cached models as immutable.

### Retention and eviction

- Uploads and ordinary single-report reads cache the report and restart its retention.
- Raw JSON downloads restart retention in cache-only hosting. With persistent stores, downloads stream directly from storage.
- When space is needed, expired and least recently used reports are evicted in batches before the new report is admitted.
- Bulk comparisons can use cached reports, but they do not add reports to the cache or restart retention.

### With and without a data store

**With persistent stores,** evicted or expired reports are loaded again on demand. A report larger than the whole budget bypasses the cache: uploads still store it, and reads still return it.

**Cache-only hosting** is supported, but retention is best effort. Eviction, expiry or a restart permanently removes report details. An ordinary read can extend retention but cannot prevent capacity eviction. Summaries can stay in the list after their details are gone; those links show a missing report. Uploads larger than the budget return HTTP 413 without adding a summary or calling the data receiver. Increase the budget or add persistent storage to accept them.

When using `MemoryCacheBdTestDataStore` through `IReportStore`, `WriteAsync` rejects reports larger than the cache budget with `InvalidOperationException`, even when other stores are configured. A rejected write preserves any existing report with the same ID.

## Upload limits

| Limit | Value |
| --- | --- |
| Request body bytes | `MaxCompressedUploadBytes`, default 10 MiB, before decompression; also applies to plain requests |
| Expanded JSON bytes | `MaxUploadBytes`, default 10 MiB, after decompression and after UTF-8 transcoding |
| Rate | `UploadsPerMinute` per instance (default 60); excess returns 429 with `Retry-After` |
| Content type | `application/json`, `text/json` or `application/*+json`, UTF-8 or UTF-16 |
| Content encoding | No encoding (plain JSON), or a single `gzip` encoding, only on `POST /bdtest/data` |

Authentication and rate limiting happen before parsing or decompression. Both byte limits must be positive and apply while streaming, including chunked requests. The expanded limit bounds both the decompressed body and its UTF-8 representation for UTF-16 uploads. It also bounds historical JSON reference expansion.

Exceeding a request or decompressed JSON byte limit returns HTTP 413. Unsupported content types, charsets or content encodings (including stacked encodings) return 415. Malformed gzip or invalid report JSON returns 400. Rejected uploads do not reach storage or the data receiver.

Raise the limits at startup when accepting larger reports:

```csharp
app.UseBDTestReportServer(options =>
{
    options.UploadApiKey = configuration["BDTest:UploadApiKey"];
    options.MaxCompressedUploadBytes = 20L * 1024 * 1024;
    options.MaxUploadBytes = 100L * 1024 * 1024;
});
```

The endpoint replaces the mutable ASP.NET Core request-body size limit with its own streaming counter, which excludes HTTP chunk framing. A reverse proxy or IIS request filtering can reject the request before BDTest runs: raise those limits too when increasing `MaxCompressedUploadBytes`, and keep a finite limit at the gateway. If host middleware has already started reading the body, its existing size limit can also apply. BDTest does not decompress requests to other endpoints.

Upload limits do not change cache capacity. With persistent stores, an accepted report larger than the cache budget can still be stored; cache-only hosts must also have a sufficient `ReportCacheSizeLimitBytes` budget.

## Bulk report comparisons

The History **Flaky tests** and **Timing variance** tabs read the latest matching runs, 20 by default and at most 50 (`compare`). `GET /bdtest/report/test-run-times` and `POST /bdtest/report/test-run-flakiness` (form field `reportIds`) accept a comma-separated list of report IDs instead.

- Up to 50 IDs. Duplicates count toward the limit but are fetched once.
- Each ID must be 1 to 128 ASCII letters, digits, dots, hyphens or underscores. `.` and `..` are invalid. Generated report GUIDs are valid.
- An invalid list returns HTTP 400 before any report is read. An empty selection redirects to the matching History tab.
- At most five reports are read concurrently.

The built-in repository reuses cached reports for these reads without adding or refreshing cache entries. Custom `IDataRepository` implementations must implement `GetDataForBulkRead` and should apply the same policy.
