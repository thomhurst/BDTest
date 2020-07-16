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

        private readonly List<TestRunSummary> _testRunSummaries = new List<TestRunSummary>();

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
            return Task.FromResult(_testRunSummaries.OrderByDescending(record => record.StartedAtDateTime).ToArray());
        }

        public Task StoreTestData(string id, BDTestOutputModel data)
        {
            _cache.Set(id, data, TimeSpan.FromHours(8));
            return Task.CompletedTask;
        }

        public Task StoreTestRunRecord(TestRunSummary testRunSummary)
        {
            _testRunSummaries.Add(testRunSummary);

            return Task.CompletedTask;
        }
    }
}