using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.RazorServer
{
    public class CosmosBDTestDataStore : IBDTestDataStore
    {
        private const string RecordDateTimeModelsKey = "RecordDateTimeModels";
        private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;";

        private readonly Container _cosmosContainer;

        public CosmosBDTestDataStore()
        {
            var cosmosClient = new CosmosClient(ConnectionString);
            
            _cosmosContainer = cosmosClient
                .CreateDatabaseIfNotExistsAsync("bdtest-reports")
                .GetAwaiter().GetResult()
                .Database
                .CreateContainerIfNotExistsAsync(new ContainerProperties("dev-debug", "/id"))
                .GetAwaiter().GetResult();
        }
        
        public async Task<BDTestOutputModel> GetTestData(string id)
        {
            var item = await _cosmosContainer.ReadItemAsync<BDTestOutputModel>(id, new PartitionKey(id));
            return item.Resource;
        }

        public async Task StoreTestData(string id, BDTestOutputModel data)
        {
            await _cosmosContainer.CreateItemAsync(data, new PartitionKey(id));
        }

        public async Task StoreTestRunRecord(TestRunOverview testRunOverview)
        {
            var records = new List<TestRunOverview>();

            try
            {
                var result = await _cosmosContainer.ReadItemAsync<CosmosRecordModel>(RecordDateTimeModelsKey,
                    new PartitionKey(RecordDateTimeModelsKey));
                
                records.AddRange(result.Resource.RecordDateTimeModels);
            }
            catch (CosmosException e) when(e.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignored - Just means our collection that we initialised is still empty!
            }
            
            records.Add(testRunOverview);

            try
            {
                await _cosmosContainer.ReplaceItemAsync(new CosmosRecordModel
                    {
                        RecordDateTimeModels = records
                    }, RecordDateTimeModelsKey,
                    new PartitionKey(RecordDateTimeModelsKey));
            }
            catch (CosmosException e) when(e.StatusCode == HttpStatusCode.NotFound)
            {
                await _cosmosContainer.CreateItemAsync(new CosmosRecordModel
                    {
                        RecordDateTimeModels = records
                    },
                    new PartitionKey(RecordDateTimeModelsKey));
            }
        }

        public async Task<TestRunOverview[]> GetAllTestRunRecords()
        {
            var records = new List<TestRunOverview>();

            try
            {
                var result = await _cosmosContainer.ReadItemAsync<CosmosRecordModel>(RecordDateTimeModelsKey,
                    new PartitionKey(RecordDateTimeModelsKey));
                
                records.AddRange(result.Resource.RecordDateTimeModels);
            }
            catch (CosmosException e) when(e.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignored - Just means our collection that we initialised is still empty!
            }

            return records.OrderByDescending(record => record.DateTime).ToArray();
        }
    }

    public class CosmosRecordModel
    {
        [JsonProperty("id")]
        public string Id { get; } = "RecordDateTimeModels";
        public List<TestRunOverview> RecordDateTimeModels { get; set; }
    }
}