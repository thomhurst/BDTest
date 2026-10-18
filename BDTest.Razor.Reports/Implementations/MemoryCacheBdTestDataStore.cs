using System.Collections.Concurrent;
using System.Text;
using BDTest.Maps;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace BDTest.Razor.Reports.Implementations;

public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore, IDisposable
{
    private readonly Lazy<MemoryCache> _testRecordCache;
    private readonly BDTestReportServerOptions _options;
    private readonly object _reportCacheLock = new();
    private long _reportCacheCapacity;
    private readonly ConcurrentDictionary<string, TestRunSummary> _testRunSummariesDictionary = new();
    private List<TestRunSummary> _testRunSummaries = new();
    private readonly object _testRunSummariesLock = new();

    /// <summary>Creates a dedicated bounded report cache with the default policy.</summary>
    /// <param name="testRecordCache">Retained for compatibility; the host cache is no longer used for full reports.</param>
    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache)
        : this(testRecordCache, new BDTestReportServerOptions())
    {
    }

    /// <summary>Creates a dedicated bounded report cache using the report server policy.</summary>
    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache, BDTestReportServerOptions options)
    {
        _options = options;
        // UseBDTestReportServer configures the policy after DI registration, before the first request.
        _testRecordCache = new Lazy<MemoryCache>(() =>
        {
            _reportCacheCapacity = options.ReportCacheSizeLimitBytes;
            return new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = _reportCacheCapacity,
                TrackStatistics = true
            });
        });
    }

    public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
    {
        lock (_reportCacheLock)
        {
            return Task.FromResult(_testRecordCache.Value.Get<BDTestOutputModel>(id));
        }
    }

    public Task DeleteTestData(string id)
    {
        lock (_reportCacheLock)
        {
            _testRecordCache.Value.Remove(id);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        return Task.FromResult(Volatile.Read(ref _testRunSummaries).AsEnumerable());
    }

    public Task StoreTestData(string id, BDTestOutputModel data)
    {
        var size = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(data));
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache.Value;
            if (size > _reportCacheCapacity)
            {
                if (_options.DataStore == null)
                {
                    throw new ReportTooLargeForCacheException();
                }

                // Persistent storage remains authoritative for reports that do not fit in memory.
                cache.Remove(id);
                return Task.CompletedTask;
            }

            cache.Remove(id);
            while (cache.GetCurrentStatistics().CurrentEstimatedSize > _reportCacheCapacity - size)
            {
                // Free least-recently-used entries in batches; always remove at least one entry.
                cache.Compact(Math.Min(1, Math.Max(0.25, 2.0 / cache.Count)));
            }

            cache.Set(id, data, new MemoryCacheEntryOptions
            {
                Size = size,
                AbsoluteExpirationRelativeToNow = _options.ReportCacheRetention
            });
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_testRecordCache.IsValueCreated)
        {
            _testRecordCache.Value.Dispose();
        }
    }

    public Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary.TryAdd(testRunSummary.RecordId, testRunSummary);

            var testRunSummariesCopy = _testRunSummariesDictionary
                .Values
                .OrderByDescending(record => record.StartedAtDateTime)
                .ToList();

            Interlocked.Exchange(ref _testRunSummaries, testRunSummariesCopy);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTestRunRecord(string id)
    {
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary.TryRemove(id, out _);

            var testRunSummariesCopy = _testRunSummaries.ToList();

            testRunSummariesCopy.RemoveAll(x => x.RecordId == id);

            Interlocked.Exchange(ref _testRunSummaries, testRunSummariesCopy);
        }

        return Task.CompletedTask;
    }
}