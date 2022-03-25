using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer;

// Cosmos only holds documents up to 2MB so this would only work for small test runs!
public class CosmosBDTestDataStore : IBDTestDataStore
{
    private const string RecordDateTimeModelsKey = "RecordDateTimeModels";

    private readonly Container _testRecordContainer;
    private readonly Container _testRunSummaryContainer;

    public CosmosBDTestDataStore(string connectionString)
    {
        var cosmosClient = new CosmosClient(connectionString);
            
        _testRecordContainer = cosmosClient
            .CreateDatabaseIfNotExistsAsync("bdtest-reports")
            .GetAwaiter().GetResult()
            .Database
            .CreateContainerIfNotExistsAsync(new ContainerProperties("test-data", "/id"))
            .GetAwaiter().GetResult();
            
        _testRunSummaryContainer = cosmosClient
            .CreateDatabaseIfNotExistsAsync("bdtest-reports")
            .GetAwaiter().GetResult()
            .Database
            .CreateContainerIfNotExistsAsync(new ContainerProperties("test-run-summary", "/id"))
            .GetAwaiter().GetResult();
    }
        
    public async Task<BDTestOutputModel> GetTestData(string id)
    {
        try
        {
            var item = await _testRecordContainer.ReadItemAsync<BDTestOutputModel>(id, new PartitionKey(id));
            return item.Resource;
        }
        catch (CosmosException e) when(e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task StoreTestData(string id, BDTestOutputModel data)
    {
        await _testRecordContainer.CreateItemAsync(data, new PartitionKey(id));
    }

    public Task DeleteTestData(string id)
    {
        throw new System.NotImplementedException();
    }

    public async Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        var records = new List<TestRunSummary>();

        try
        {
            var result = await _testRunSummaryContainer.ReadItemAsync<CosmosRecordModel>(RecordDateTimeModelsKey,
                new PartitionKey(RecordDateTimeModelsKey));

            records.AddRange(result.Resource.RecordDateTimeModels);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Ignored - Just means our collection that we initialised is still empty!
        }

        records.Add(testRunSummary);

        // Only keep the last 100 records
        var recordsToSave = records
            .OrderByDescending(record => record.StartedAtDateTime)
            .Take(100)
            .ToList();

        try
        {
            await _testRunSummaryContainer.ReplaceItemAsync(new CosmosRecordModel
                {
                    RecordDateTimeModels = recordsToSave
                }, RecordDateTimeModelsKey,
                new PartitionKey(RecordDateTimeModelsKey));
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            await _testRunSummaryContainer.CreateItemAsync(new CosmosRecordModel
                {
                    RecordDateTimeModels = recordsToSave
                },
                new PartitionKey(RecordDateTimeModelsKey));
        }

        // Delete purged records
        await Task.WhenAll(
            records.Except(recordsToSave)
                .Select(record =>
                    _testRecordContainer.DeleteItemAsync<dynamic>(record.RecordId,
                        new PartitionKey(record.RecordId))));
    }

    public Task DeleteTestRunRecord(string id)
    {
        throw new System.NotImplementedException();
    }

    public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        var records = new List<TestRunSummary>();

        try
        {
            var result = await _testRunSummaryContainer.ReadItemAsync<CosmosRecordModel>(RecordDateTimeModelsKey,
                new PartitionKey(RecordDateTimeModelsKey));
                
            records.AddRange(result.Resource.RecordDateTimeModels);
        }
        catch (CosmosException e) when(e.StatusCode == HttpStatusCode.NotFound)
        {
            // Ignored - Just means our collection that we initialised is still empty!
        }

        return records.ToArray();
    }
}

public class CosmosRecordModel
{
    [JsonProperty("id")]
    public string Id { get; } = "RecordDateTimeModels";
    public List<TestRunSummary> RecordDateTimeModels { get; set; }
}