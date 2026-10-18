---
title: Persistent storage
description: Keep reports across restarts by implementing IBDTestDataStore, with an Azure Blob Storage example.
---

# Persistent storage

Without a data store, the report server keeps reports only in a bounded memory cache. To keep them, implement `IBDTestDataStore` and set it as the `DataStore` option.

## The interface

```csharp
namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestDataStore
{
    Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken);
    Task StoreTestData(string id, BDTestOutputModel data);
    Task DeleteTestData(string id);

    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreTestRunRecord(TestRunSummary testRunSummary);
    Task DeleteTestRunRecord(string id);
}
```

| Method | Purpose |
| --- | --- |
| `StoreTestData` | Store a full report under the given ID. Use that ID to retrieve it later. |
| `GetTestData` | Return the full report with that ID, or `null` if it does not exist. |
| `DeleteTestData` | Delete a full report. |
| `StoreTestRunRecord` | Store a small run summary (counts, times, environment, tag, branch) under `testRunSummary.RecordId`. |
| `GetAllTestRunRecords` | Return the summaries to list on the test run page. Apply your own limit or retention here, for example "the latest 50" or "the last 30 days". |
| `DeleteTestRunRecord` | Delete a summary. |

Store reports and summaries as JSON. Use `BDTestJsonHelper` for `BDTestOutputModel` and `ReportJsonContext.Default.TestRunSummary` (namespace `BDTest.Razor.Reports.Helpers`) for `TestRunSummary`.

## Registering the store

```csharp
app.UseBDTestReportServer(options =>
{
    options.DataStore = new AzureStorageDataStore(connectionString);
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});
```

The full report cache sits in front of the store. Uploads write to both; reads try the cache first and load from the store on a miss. See [Caching and limits](caching-and-limits.md).

## Example: Azure Blob Storage

This is a simplified version of the store in the [sample server](sample-server.md). It uses the `Azure.Storage.Blobs` package.

```csharp
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

public class AzureStorageDataStore : IBDTestDataStore
{
    private readonly BlobContainerClient _summaries;
    private readonly BlobContainerClient _reports;

    public AzureStorageDataStore(string connectionString)
    {
        var blobService = new BlobServiceClient(connectionString);
        _summaries = blobService.GetBlobContainerClient("test-runs-summaries");
        _reports = blobService.GetBlobContainerClient("test-data");
    }

    // Call once at startup, before serving requests.
    public async Task InitializeAsync()
    {
        await _summaries.CreateIfNotExistsAsync();
        await _reports.CreateIfNotExistsAsync();
    }

    public async Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _reports.GetBlockBlobClient(id).DownloadAsync(cancellationToken);
            using var content = download.Value.Content;
            return await BDTestJsonHelper.DeserializeAsync(content, cancellationToken);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public Task StoreTestData(string id, BDTestOutputModel data) =>
        _reports.GetBlobClient(id).UploadAsync(BinaryData.FromString(BDTestJsonHelper.Serialize(data)), overwrite: true);

    public Task DeleteTestData(string id) => _reports.DeleteBlobIfExistsAsync(id);

    public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        var blobs = new List<BlobItem>();
        await foreach (var blob in _summaries.GetBlobsAsync())
        {
            blobs.Add(blob);
        }

        var summaries = new List<TestRunSummary>();
        foreach (var blob in blobs.OrderByDescending(b => b.Properties.CreatedOn).Take(50))
        {
            var download = await _summaries.GetBlockBlobClient(blob.Name).DownloadAsync();
            using var content = download.Value.Content;
            summaries.Add(await JsonSerializer.DeserializeAsync(content, ReportJsonContext.Default.TestRunSummary));
        }

        return summaries;
    }

    public Task StoreTestRunRecord(TestRunSummary summary) =>
        _summaries.GetBlobClient(summary.RecordId).UploadAsync(
            BinaryData.FromString(JsonSerializer.Serialize(summary, ReportJsonContext.Default.TestRunSummary)),
            overwrite: true);

    public Task DeleteTestRunRecord(string id) => _summaries.DeleteBlobIfExistsAsync(id);
}
```

The sample server's real store also orders summaries by blob creation time, downloads them in parallel and tolerates blobs deleted mid-listing. See [`AzureStorageDataStore.cs`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/AzureStorageDataStore.cs).

## Retention

The report server never deletes reports by age. Configure retention in your storage, for example with a lifecycle policy. The sample includes [`azure-storage-lifecycle.json`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/azure-storage-lifecycle.json), a 30-day rule for both containers. Summary and report deletion is not atomic, so a summary can briefly link to a missing report.

## Bulk reads

Comparison pages read many reports at once. If you implement `IDataRepository` yourself, override `GetDataForBulkRead` to control caching for those reads; by default it calls `GetData`. See [Caching and limits](caching-and-limits.md#bulk-report-comparisons).
