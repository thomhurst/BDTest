---
title: Persistent storage
description: Implement cancellable report byte storage and a paged run index.
---

# Persistent storage

Configure `IReportStore` and `IRunIndexStore` from `BDTest.Razor.Reports.Storage` to keep reports across restarts. One class or separate services can implement them. Configure both before resolving `IDataRepository` or serving requests:

```csharp
app.UseBDTestReportServer(options =>
{
    options.ReportStore = reportStore;
    options.RunIndexStore = runIndexStore;
});
```

The repository owns deserialization, normalization, caching and conversion to `TestRunSummary` view models. Stores move UTF-8 JSON bytes and `RunIndexEntry` metadata. Storage contracts do not reference Razor view models.

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

`IBDTestDataStore` and `options.DataStore` remain as a one-release migration bridge. The repository automatically wraps old implementations with `LegacyDataStoreAdapter`; a store implementing the new interfaces is used directly. You can assign an explicit adapter to both new options. Adapter upserts remove the old summary before storing its replacement; that pair is not atomic. Legacy stores still load whole models and all summaries; the adapter cannot interrupt their uncancellable in-flight operations and raw downloads must serialize their models. See [Storage migration](../migration/report-storage.md).

## Retention and custom repositories

The report server never deletes reports by age. Configure retention in storage, for example with the sample's [`azure-storage-lifecycle.json`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/azure-storage-lifecycle.json). Summary and report deletion is not atomic, so a summary can briefly link to a missing report.

Custom `IDataRepository` implementations must implement every method, including `GetDataForBulkRead`, `OpenReadAsync` and `ListAsync`. Every operation accepts cancellation; `StoreData` takes the report ID first. Bulk reads should avoid adding or refreshing full reports in cache. See [Caching and limits](caching-and-limits.md#bulk-report-comparisons).
