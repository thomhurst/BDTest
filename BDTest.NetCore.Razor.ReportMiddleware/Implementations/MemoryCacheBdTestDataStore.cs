using System.Collections.Concurrent;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations;

public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore
{
    private readonly IMemoryCache _testRecordCache;

    private readonly object _testRunSummariesLock = new();

    private readonly ConcurrentDictionary<string, TestRunSummary> _testRunSummariesDictionary = new();
    private List<TestRunSummary> _testRunSummaries = new();

    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache)
    {
        _testRecordCache = testRecordCache;
    }

    public Task<BDTestOutputModel> GetTestData(string id)
    {
        if (_testRecordCache.TryGetValue<BDTestOutputModel>(id, out var model))
        {
            return Task.FromResult(model);
        }

        return Task.FromResult<BDTestOutputModel>(null);
    }

    public Task DeleteTestData(string id)
    {
        _testRecordCache.Remove(id);
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
        _testRecordCache.Set(id, data, TimeSpan.FromHours(8));
        return Task.CompletedTask;
    }

    public Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary.TryAdd(testRunSummary.RecordId, testRunSummary);

            _testRunSummaries = _testRunSummariesDictionary
                .Values
                .OrderByDescending(record => record.StartedAtDateTime)
                .ToList();
        }

        return Task.CompletedTask;
    }

    public Task DeleteTestRunRecord(string id)
    {
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary.TryRemove(id, out _);
            _testRunSummaries.RemoveAll(x => x.RecordId == id);
        }
            
        return Task.CompletedTask;
    }
}