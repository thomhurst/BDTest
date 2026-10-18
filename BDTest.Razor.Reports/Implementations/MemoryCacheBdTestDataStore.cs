using System.Collections.Concurrent;
using System.Text.Json;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Storage;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

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
        => GetTestData(id, cancellationToken, refreshRetention: false);

    Task<BDTestOutputModel> IMemoryCacheBdTestDataStore.GetTestDataAndRefreshRetention(string id,
        CancellationToken cancellationToken)
        => GetTestData(id, cancellationToken, refreshRetention: true);

    private async Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken, bool refreshRetention)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CachedReport report;
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache.Value;
            report = cache.Get<CachedReport>(id);
            if (refreshRetention && report != null) SetCacheEntry(cache, id, report);
        }
        var value = report?.Value;
        if (value is not byte[] bytes) return value as BDTestOutputModel;
        using var stream = new MemoryStream(bytes, writable: false);
        return await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken);
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
        long size;
        using (var counter = new ByteCountingStream())
        {
            JsonSerializer.Serialize(counter, data, BDTestJsonContext.Default.BDTestOutputModel);
            size = counter.Length;
        }
        CacheReport(id, data, size);
        return Task.CompletedTask;
    }

    private void CacheReport(string id, object data, long size, bool allowOversizedBypass = true)
    {
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache.Value;
            if (size > _reportCacheCapacity)
            {
                if (!allowOversizedBypass || (_options.DataStore == null && _options.ReportStore == null))
                {
                    throw new ReportTooLargeForCacheException();
                }

                // Persistent storage remains authoritative for reports that do not fit in memory.
                cache.Remove(id);
                return;
            }

            cache.Remove(id);
            while (cache.GetCurrentStatistics().CurrentEstimatedSize > _reportCacheCapacity - size)
            {
                // Free least-recently-used entries in batches; always remove at least one entry.
                cache.Compact(Math.Min(1, Math.Max(0.25, 2.0 / cache.Count)));
            }

            SetCacheEntry(cache, id, new CachedReport(data, size));
        }
    }

    private void SetCacheEntry(MemoryCache cache, string id, CachedReport report)
    {
        cache.Set(id, report, new MemoryCacheEntryOptions
        {
            Size = report.Size,
            AbsoluteExpirationRelativeToNow = _options.ReportCacheRetention
        }.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (reason is EvictionReason.Expired or EvictionReason.Capacity
                && _options.DataStore == null && _options.ReportStore == null && _options.AttachmentStore != null
                && AttachmentStorage.IsValidId((string)key))
                _ = DeleteExpiredAttachments((string)key);
        }));
    }

    private async Task DeleteExpiredAttachments(string reportId)
    {
        await _options.AttachmentGate.WaitAsync();
        try
        {
            lock (_reportCacheLock)
            {
                if (_testRecordCache.Value.TryGetValue(reportId, out _)) return;
            }
            await _options.AttachmentStore.DeleteReportAsync(reportId);
        }
        catch (Exception exception)
        {
            _options.Logger?.LogError(exception, "Failed to remove attachments for expired report {ReportId}", reportId);
        }
        finally { _options.AttachmentGate.Release(); }
    }

    private sealed record CachedReport(object Value, long Size);

    // Serialize UTF-8 incrementally; retain only the byte count, never the JSON payload.
    private sealed class ByteCountingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        private long _length;
        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => _length += count;
        public override void Write(ReadOnlySpan<byte> buffer) => _length += buffer.Length;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
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

    public async Task<Stream> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object value;
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache.Value;
            var report = cache.Get<CachedReport>(reportId);
            if (report != null) SetCacheEntry(cache, reportId, report);
            value = report?.Value;
        }
        if (value is byte[] bytes) return new MemoryStream(bytes, writable: false);
        if (value is not BDTestOutputModel model) return null;
        var stream = new MemoryStream();
        try
        {
            await JsonSerializer.SerializeAsync(stream, model, BDTestJsonContext.Default.BDTestOutputModel, cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await utf8Json.ReadAsync(chunk, cancellationToken)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length + count > _options.ReportCacheSizeLimitBytes)
            {
                throw new ReportTooLargeForCacheException();
            }
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        CacheReport(reportId, buffer.ToArray(), buffer.Length, allowOversizedBypass: false);
    }

    Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_reportCacheLock)
        {
            var exists = _testRecordCache.Value.TryGetValue(reportId, out _);
            _testRecordCache.Value.Remove(reportId);
            return Task.FromResult(exists);
        }
    }

    public Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RunIndexPaging.Apply(Volatile.Read(ref _testRunSummaries)
            .Select(entry => entry.ToRunIndexEntry()), query));
    }

    public Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary[entry.RecordId] = entry.ToTestRunSummary();
            Interlocked.Exchange(ref _testRunSummaries, _testRunSummariesDictionary.Values
                .OrderByDescending(record => record.StartedAtDateTime)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal).ToList());
        }
        return Task.CompletedTask;
    }

    Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_testRunSummariesLock)
        {
            var removed = _testRunSummariesDictionary.TryRemove(reportId, out _);
            Interlocked.Exchange(ref _testRunSummaries, _testRunSummaries.Where(record => record.RecordId != reportId).ToList());
            return Task.FromResult(removed);
        }
    }
}
