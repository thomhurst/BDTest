using System.Collections.Concurrent;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations;

public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore
{
    private readonly IMemoryCache _testRecordCache;
    private readonly ConcurrentDictionary<string, TestRunSummary> _testRunSummariesDictionary = new();
    private List<TestRunSummary> _testRunSummaries = new();

    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache)
    {
        _testRecordCache = testRecordCache;
    }

    public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
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
        return Task.FromResult(_testRunSummaries.AsEnumerable());
    }

    public Task StoreTestData(string id, BDTestOutputModel data)
    {
        _testRecordCache.Set(id, data, TimeSpan.FromHours(8));
        return Task.CompletedTask;
    }

    public Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        var testRunSummariesCopy = _testRunSummariesDictionary
                .Values
                .OrderByDescending(record => record.StartedAtDateTime)
                .ToList();
        
        Interlocked.Exchange(ref _testRunSummaries, testRunSummariesCopy);
        
        _testRunSummariesDictionary.TryAdd(testRunSummary.RecordId, testRunSummary);

        return Task.CompletedTask;
    }

    public Task DeleteTestRunRecord(string id)
    {
        _testRunSummariesDictionary.TryRemove(id, out _);
        
        var testRunSummariesCopy = _testRunSummaries.ToList();

        testRunSummariesCopy.RemoveAll(x => x.RecordId == id);

        Interlocked.Exchange(ref _testRunSummaries, testRunSummariesCopy);

        return Task.CompletedTask;
    }
}