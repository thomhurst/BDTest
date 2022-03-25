using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer;

public class AzureStorageDataStore : IBDTestDataStore
{
    private readonly BlobContainerClient _testRunSummariesContainer;
    private readonly BlobContainerClient _testDataContainer;
    private readonly JsonSerializer _jsonSerializer = JsonSerializer.Create();

    public AzureStorageDataStore(AzureStorageConfig azureStorageConfig)
    {
        var connectionString = azureStorageConfig.ConnectionString;
        var blobClient = new BlobServiceClient(connectionString);
            
        _testRunSummariesContainer = blobClient.GetBlobContainerClient("test-runs-summaries");
        _testRunSummariesContainer.CreateIfNotExists();
            
        _testDataContainer = blobClient.GetBlobContainerClient("test-data");
        _testDataContainer.CreateIfNotExists();
    }
        
    public async Task<BDTestOutputModel> GetTestData(string id)
    {
        try
        {
            var blobData = await _testDataContainer.GetBlockBlobClient(id).DownloadAsync();
            return _jsonSerializer.Deserialize<BDTestOutputModel>(new JsonTextReader(new StreamReader(blobData.Value.Content)));
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
        var testRunSummaries = new List<TestRunSummary>();
        var amountToTake = 50;

        var count = 0;
        await foreach (var blobItem in _testRunSummariesContainer.GetBlobsAsync())
        {
            count++;
            var downloadAsync = await _testRunSummariesContainer.GetBlockBlobClient(blobItem.Name).DownloadAsync();
            var testRunSummary = _jsonSerializer.Deserialize<TestRunSummary>(new JsonTextReader(new StreamReader(downloadAsync.Value.Content)));

            if (blobItem.Properties.CreatedOn < DateTimeOffset.UtcNow - TimeSpan.FromDays(30))
            {
                await DeleteBlobItem(blobItem);
            }
            else
            {
                testRunSummaries.Add(testRunSummary);
            }

            if (count == amountToTake)
            {
                break;
            }
        }

        return testRunSummaries.ToArray();
    }

    private async Task DeleteBlobItem(BlobItem blobItem)
    {
        await Task.WhenAll(
            DeleteTestData(blobItem.Name),
            DeleteTestRunRecord(blobItem.Name)
        );
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

    public Task InitializeAsync()
    {
        return GetAllTestRunRecords();
    }
}