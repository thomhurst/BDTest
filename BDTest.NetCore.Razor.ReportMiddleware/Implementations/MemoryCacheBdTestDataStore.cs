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
        private readonly IMemoryCache _cache;

        private readonly object _testRunSummariesLock = new object();
        private List<TestRunSummary> _testRunSummaries = new List<TestRunSummary>();

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

        public Task DeleteTestData(string id)
        {
            _cache.Remove(id);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            lock (_testRunSummariesLock)
            {
                return Task.FromResult(_testRunSummaries.AsEnumerable());
            }
        }

        public Task StoreTestData(string id, BDTestOutputModel data)
        {
            _cache.Set(id, data, TimeSpan.FromHours(8));
            return Task.CompletedTask;
        }

        public Task StoreTestRunRecord(TestRunSummary testRunSummary)
        {
            lock (_testRunSummariesLock)
            {
                _testRunSummaries.Add(testRunSummary);

                _testRunSummaries = _testRunSummaries.OrderByDescending(record => record.StartedAtDateTime).ToList();
            }

            return Task.CompletedTask;
        }

        public Task DeleteTestRunRecord(string id)
        {
            lock (_testRunSummariesLock)
            {
                var itemToDelete = _testRunSummaries.FirstOrDefault(item => item.RecordId == id);

                if (itemToDelete == null)
                {
                    return Task.CompletedTask;
                }

                _testRunSummaries.Remove(itemToDelete);
            }
            
            return Task.CompletedTask;
        }
    }
}