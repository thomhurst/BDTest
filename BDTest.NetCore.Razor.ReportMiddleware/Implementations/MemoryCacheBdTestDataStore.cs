using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations
{
    public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore
    {
        private const string RecordDateTimeModelsKey = "RecordDateTimeModels";
        private readonly IMemoryCache _cache;

        private readonly MemoryCacheEntryOptions _neverRemoveMemoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        };

        public MemoryCacheBdTestDataStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<BDTestOutputModel> GetTestData(string id)
        {
            if (_cache.TryGetValue<BDTestOutputModel>(id, out var model))
            {
                return Task.FromResult(model);
            }

            return Task.FromResult(null as BDTestOutputModel);
        }

        public Task<TestRunSummary[]> GetAllTestRunRecords()
        {
            var recordDateTimeModels = new List<TestRunSummary>();
            
            if(_cache.TryGetValue(RecordDateTimeModelsKey, out var recordDateTimeModelsAsObject))
            {
                recordDateTimeModels.AddRange(recordDateTimeModelsAsObject as IEnumerable<TestRunSummary> ?? Array.Empty<TestRunSummary>());
            }

            return Task.FromResult(recordDateTimeModels.OrderByDescending(record => record.StartedAtDateTime).ToArray());
        }

        public Task StoreTestData(string id, BDTestOutputModel data)
        {
            _cache.Set(id, data, TimeSpan.FromHours(8));
            return Task.CompletedTask;
        }

        public Task StoreTestRunRecord(TestRunSummary testRunSummary)
        {
            var recordDateTimeModels = new List<TestRunSummary>();
            
            if(_cache.TryGetValue(RecordDateTimeModelsKey, out var recordDateTimeModelsAsObject))
            {
                recordDateTimeModels.AddRange(recordDateTimeModelsAsObject as IEnumerable<TestRunSummary> ?? Array.Empty<TestRunSummary>());
            }
            
            recordDateTimeModels.Add(testRunSummary);

            _cache.Set(RecordDateTimeModelsKey, recordDateTimeModels, _neverRemoveMemoryCacheEntryOptions);
            
            return Task.CompletedTask;
        }
    }
}