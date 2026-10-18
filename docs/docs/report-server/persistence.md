---
title: Persistent storage
description: Implement cancellable report byte storage and a paged run index.
---

# Persistent storage

Register `IReportStore` and `IRunIndexStore` from `BDTest.Razor.Reports.Storage` to keep reports across restarts. One class or separate services can implement them. Register both before building the host:

```csharp
builder.Services.AddSingleton<IReportStore>(reportStore);
builder.Services.AddSingleton<IRunIndexStore>(runIndexStore);
builder.Services.AddBDTestReports().BindConfiguration("BDTest");
```

The repository owns deserialization, normalization, caching and conversion to `TestRunSummary` view models. Stores move UTF-8 JSON bytes and `RunIndexEntry` metadata. Storage contracts do not reference Razor view models.

Configure report read authorization as shown in [Security](security.md#requiring-sign-in) before using these option snippets.

## Report bytes

```csharp
public interface IReportStore
{
    Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken);
    Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken);
}
```

`OpenReadAsync` returns `null` when missing. The caller owns the returned stream and must dispose it. The stream need not support seeking. `WriteAsync` replaces any existing report, reads from the input's current position, and leaves the caller's stream open. Delete returns whether an item existed. Propagate cancellation to every storage operation.

For example, a blob implementation moves bytes without knowing the report schema:

```csharp
public sealed class BlobReportStore(BlobContainerClient container) : IReportStore
{
    public async Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var download = await container.GetBlockBlobClient(reportId)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await container.GetBlockBlobClient(reportId)
            .UploadAsync(utf8Json, cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deleted = await container.DeleteBlobIfExistsAsync(reportId,
            cancellationToken: cancellationToken);
        return deleted.Value;
    }
}
```

This example uses `Azure`, `Azure.Storage.Blobs` and `Azure.Storage.Blobs.Specialized` from `Azure.Storage.Blobs`. Initialize containers before accepting requests.

## Run index

```csharp
public interface IRunIndexStore
{
    Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken);
    Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken);
}
```

Persist `RunIndexEntry` separately from report bytes. It contains `RecordId`, start/finish times, `Status`, `RunIndexCounts`, tag, environment, branch, machine and version. Upsert replaces metadata for the same `RecordId`. Report writes and index updates are separate operations, not a transaction.

`RunIndexQuery` specifies `PageSize` (1–500, default 50), an opaque `ContinuationToken`, and optional `BranchName`, `Environment` and `Tag` filters. Filters use exact, case-sensitive ordinal equality. Order by `StartedAtDateTime` descending, then `RecordId` ascending with ordinal comparison. Return entries in `RunIndexPage.Entries`, plus a token for the next page, or `null` at the end. Continue until the token is null, including when a page is empty. Reuse the same filters for subsequent pages. Tokens are navigation cursors, not authorization credentials.

`RunIndexPaging.Apply(entries, query)` implements this contract for a collection. Its keyset tokens remain valid if the cursor record is deleted and reject a different filter set. For a database, translate filters and ordering into an indexed query and return your own opaque cursor. Index listing must not load full reports.

The built-in repository loads persisted history once through every page, under a cancellable semaphore. Uploads during loading are preserved, and deletes coordinate with that snapshot so deleted records are not reintroduced. `IDataRepository.ListAsync` pages combined cached history. Existing report-list and chart pages still consume the full metadata list; storage paging does not change their URLs.

## Azure and memory implementations

The sample server's [`AzureStorageDataStore.cs`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/AzureStorageDataStore.cs) implements both interfaces. It streams report blobs directly, honors cancellation and downloads index entries with at most eight concurrent requests. Its sample index scans all summary blobs for each query before sorting and filtering by stored run start time. Large installations should use an indexed service for `IRunIndexStore`. Existing summary JSON remains readable.

`MemoryCacheBdTestDataStore` also implements both interfaces, with bounded report storage and an in-memory index. Byte writes preserve original JSON. The repository can still cache parsed models for ordinary report pages. Without persistent storage, eviction, expiry and restart lose report details.

## Raw downloads and legacy stores

`GET /bdtest/report/{id}/raw-json-data` copies the persistent stream directly to the response, preserving stored bytes without deserializing and serializing the report. MVC disposes the stream after the response, including failed copies. Ordinary pages continue to support historical Newtonsoft JSON through the repository's existing reader.

`IBDTestDataStore` remains as a one-release migration bridge. Register it as a DI service; the repository automatically wraps old implementations with `LegacyDataStoreAdapter`. Explicit `IReportStore` and `IRunIndexStore` registrations take precedence; register both. You can also register one explicit adapter under both interfaces. Adapter upserts remove the old summary before storing its replacement; that pair is not atomic. Legacy stores still load whole models and all summaries; the adapter cannot interrupt their uncancellable in-flight operations and raw downloads must serialize their models. See [Storage migration](../migration/report-storage.md).

## Retention and custom repositories

The report server never deletes reports by age. Configure retention in storage, for example with the sample's [`azure-storage-lifecycle.json`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/azure-storage-lifecycle.json). Summary and report deletion is not atomic, so a summary can briefly link to a missing report.

Custom `IDataRepository` implementations must implement every method, including `GetDataForBulkRead`, `OpenReadAsync` and `ListAsync`. Every operation accepts cancellation; `StoreData` takes the report ID first. Bulk reads should avoid adding or refreshing full reports in cache. See [Caching and limits](caching-and-limits.md#bulk-report-comparisons).

## Attachments

Attachments use the independent `BDTest.Razor.Reports.Interfaces.IBDTestAttachmentStore` contract. Existing report stores require no changes. Register the attachment store before building the host; report uploads containing attachments return 400 if no store is configured.

```csharp
using BDTest.Razor.Reports.Storage;
using BDTest.Razor.Reports.Interfaces;

builder.Services.AddSingleton<IBDTestAttachmentStore>(
    new FileBDTestAttachmentStore("/var/lib/bdtest/attachments"));
builder.Services.AddBDTestReports(options =>
{
    options.UploadApiKey = Environment.GetEnvironmentVariable("BDTEST_REPORT_UPLOAD_API_KEY");
    options.MaximumAttachmentBytes = 10 * 1024 * 1024;
    options.MaximumReportAttachmentBytes = 100 * 1024 * 1024;
    options.MaximumAttachmentsPerReport = 1000;
});
```

The file store creates its root if needed. Give only the host filesystem access, keep this directory outside static-file roots, and manage its lifetime and backups with the report store. Untrusted filesystem writers must not be able to create symlinks or replace directories. Only lowercase 32-character hexadecimal report/attachment IDs form storage keys; names and MIME types never form paths. Each write stages content in a random temporary file and publishes it atomically after validating the exact length. Failed or cancelled writes remove partial files and preserve previously completed content. Deletion first renames a report directory to a report-scoped `.deleting` directory. If removal fails, a later `DeleteReportAsync` call discovers and removes the remaining content, including after a server restart; failures continue to propagate until cleanup succeeds.

```csharp
public interface IBDTestAttachmentStore
{
    Task<Stream?> OpenReadAsync(string reportId, string attachmentId,
        CancellationToken cancellationToken = default);
    Task WriteAsync(string reportId, string attachmentId, Stream content, long length,
        CancellationToken cancellationToken = default);
    Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default);
}
```

Readers own returned streams; return null for missing bytes. Writers read from the current position, leave input open, and must reject short or excess input without publishing partial content. All operations observe cancellation. `DeleteReportAsync` removes only that report's content and succeeds when absent. `AttachmentStorage.CopyExactlyAsync` provides a bounded copy helper. `BDTestAttachmentHelper.GetAttachments(report)` enumerates scenario, step and outline-example metadata, including retained retries.

The sample Azure server configures its `AzureStorageDataStore` as the attachment store. Blobs live under `test-data/{reportId}/attachments/{attachmentId}` in the existing report container. Downloads stream directly; writes stage and validate bytes before committing a blob. Deletion lists every page under the exact report prefix. The supplied Azure lifecycle policy covers both report and attachment blobs; apply that policy in Azure to enable retention. Since blobs have individual modification times, expiry is eventual rather than transactional.

The built-in repository coordinates attachment writes and report deletion within a server instance. Deletion removes attachment content before report JSON and index entries; failures propagate so deletion can be retried. Historical reports without generated attachment IDs still delete normally. For cache-only reports, expiry and capacity eviction schedule attachment cleanup without touching other reports. Cleanup failures use the host's logging services. Persistent reports keep their attachments when only their memory-cache copy expires. Custom repositories and external retention jobs must call `DeleteReportAsync` as part of their own deletion path. A cache-only host loses its report index on restart; arrange directory cleanup on host shutdown/restart or persist report JSON alongside file attachments.
