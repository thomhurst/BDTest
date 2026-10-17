using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BDTest.Maps;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer;

public class AzureStorageDataStore : IBDTestDataStore, IAsyncDataStoreInitializer
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
        
    public async Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
    {
        try
        {
            var blobData = await _testDataContainer.GetBlockBlobClient(id).DownloadAsync(cancellationToken);
            using var content = blobData.Value.Content;
            using var reader = new JsonTextReader(new StreamReader(content));
            return JsonSerializer.Create().Deserialize<BDTestOutputModel>(reader);
        }
        catch (RequestFailedException e) when(e.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteTestData(string id)
    {
        return _testRunSummariesContainer.DeleteBlobIfExistsAsync(id);
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
                    using var reader = new JsonTextReader(new StreamReader(content));
                    // Each concurrent download needs its own serializer and reader.
                    summaries[index] = JsonSerializer.Create().Deserialize<TestRunSummary>(reader);
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
        return _testDataContainer.DeleteBlobIfExistsAsync(id);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _testRunSummariesContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _testDataContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }
}