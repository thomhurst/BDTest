using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BDTest.Maps;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using System.Text.Json;
using BDTest.Helpers;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Storage;

namespace BDTest.ReportGenerator.RazorServer;

public class AzureStorageDataStore : IBDTestDataStore, IReportStore, IRunIndexStore, IAsyncDataStoreInitializer
{
    private readonly BlobContainerClient _testRunSummariesContainer;
    private readonly BlobContainerClient _testDataContainer;
    private const int MaximumSummaries = 50;
    private const int MaximumConcurrentDownloads = 8;

    public AzureStorageDataStore(BlobContainerClient testRunSummariesContainer, BlobContainerClient testDataContainer)
    {
        _testRunSummariesContainer = testRunSummariesContainer;
        _testDataContainer = testDataContainer;
    }

    public AzureStorageDataStore(AzureStorageConfig azureStorageConfig)
    {
        var connectionString = azureStorageConfig.ConnectionString;
        var blobClient = new BlobServiceClient(connectionString);
            
        _testRunSummariesContainer = blobClient.GetBlobContainerClient("test-runs-summaries");
            
        _testDataContainer = blobClient.GetBlobContainerClient("test-data");
    }

    public async Task<Stream> OpenReadAsync(string reportId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var download = await _testDataContainer.GetBlockBlobClient(reportId)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _testDataContainer.GetBlockBlobClient(reportId)
            .UploadAsync(utf8Json, cancellationToken: cancellationToken);
    }

    async Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deleted = await _testDataContainer.DeleteBlobIfExistsAsync(reportId, cancellationToken: cancellationToken);
        return deleted.Value;
    }

    public async Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Validate the query before making any storage requests.
        RunIndexPaging.Apply(Array.Empty<RunIndexEntry>(), query);
        var entries = new List<RunIndexEntry>();
        await foreach (var page in _testRunSummariesContainer.GetBlobsAsync(cancellationToken: cancellationToken).AsPages())
        {
            var pageEntries = new RunIndexEntry[page.Values.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, page.Values.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaximumConcurrentDownloads,
                    CancellationToken = cancellationToken
                },
                async (index, token) =>
                {
                    try
                    {
                        var download = await _testRunSummariesContainer.GetBlockBlobClient(page.Values[index].Name)
                            .DownloadStreamingAsync(cancellationToken: token);
                        using var content = download.Value.Content;
                        pageEntries[index] = await JsonSerializer.DeserializeAsync(content,
                            ReportJsonContext.Default.RunIndexEntry, token);
                    }
                    catch (RequestFailedException exception) when (exception.Status == 404)
                    {
                        // A lifecycle policy can remove an entry between listing and downloading.
                    }
                });
            entries.AddRange(pageEntries.Where(entry => entry != null));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return RunIndexPaging.Apply(entries, query);
    }

    public async Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var json = new MemoryStream();
        await JsonSerializer.SerializeAsync(json, entry, ReportJsonContext.Default.RunIndexEntry, cancellationToken);
        json.Position = 0;
        await _testRunSummariesContainer.GetBlockBlobClient(entry.RecordId)
            .UploadAsync(json, cancellationToken: cancellationToken);
    }

    async Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deleted = await _testRunSummariesContainer.DeleteBlobIfExistsAsync(reportId, cancellationToken: cancellationToken);
        return deleted.Value;
    }
        
    public async Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
    {
        try
        {
            var blobData = await _testDataContainer.GetBlockBlobClient(id).DownloadAsync(cancellationToken);
            using var content = blobData.Value.Content;
            return await BDTestJsonHelper.DeserializeAsync(content, cancellationToken);
        }
        catch (RequestFailedException e) when(e.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteTestData(string id)
    {
        return _testDataContainer.DeleteBlobIfExistsAsync(id);
    }

    public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        // Blob names are report IDs, not timestamps. Inspect every listing page before
        // downloading, retaining only the newest candidates between pages.
        var newestBlobs = Array.Empty<BlobItem>();
        await foreach (var page in _testRunSummariesContainer.GetBlobsAsync().AsPages())
        {
            newestBlobs = newestBlobs.Concat(page.Values)
                .OrderByDescending(blob => blob.Properties.CreatedOn ?? blob.Properties.LastModified ?? DateTimeOffset.MinValue)
                .ThenBy(blob => blob.Name, StringComparer.Ordinal)
                .Take(MaximumSummaries)
                .ToArray();
        }

        var summaries = new TestRunSummary[newestBlobs.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, newestBlobs.Length),
            new ParallelOptions { MaxDegreeOfParallelism = MaximumConcurrentDownloads },
            async (index, cancellationToken) =>
            {
                try
                {
                    var download = await _testRunSummariesContainer.GetBlockBlobClient(newestBlobs[index].Name)
                        .DownloadAsync(cancellationToken);
                    using var content = download.Value.Content;
                    summaries[index] = await JsonSerializer.DeserializeAsync(content, ReportJsonContext.Default.TestRunSummary, cancellationToken);
                }
                catch (RequestFailedException exception) when (exception.Status == 404)
                {
                    // An administrator or lifecycle policy can delete a blob after listing.
                }
            });

        return summaries.Where(summary => summary != null).ToArray();
    }

    public Task StoreTestData(string id, BDTestOutputModel data)
    {
        return _testDataContainer.GetBlockBlobClient(id).UploadAsync(data.AsStream());
    }

    public Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        return _testRunSummariesContainer.GetBlockBlobClient(testRunSummary.RecordId).UploadAsync(testRunSummary.AsStream());
    }

    public Task DeleteTestRunRecord(string id)
    {
        return _testRunSummariesContainer.DeleteBlobIfExistsAsync(id);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _testRunSummariesContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _testDataContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }
}
