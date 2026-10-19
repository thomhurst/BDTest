using System.Collections.Concurrent;
using System.Text.Json;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Storage;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Internal;

namespace BDTest.Razor.Reports.Implementations;

public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore, IDisposable
{
    private static readonly JsonSerializerOptions PageSizeOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<BDTest.Contracts.Status>() }
    };
    private readonly MemoryCache _testRecordCache;
    private readonly MemoryCache _pageCache;
    private bool _disposed;
    private readonly BDTestReportOptions _options;
    private readonly object _reportCacheLock = new();
    private readonly long _reportCacheCapacity;
    private readonly bool _hasPersistentStorage;
    private readonly IBDTestAttachmentStore? _attachmentStore;
    private readonly ReportAttachmentGate _attachmentGate;
    private readonly ILogger<MemoryCacheBdTestDataStore>? _logger;
    private readonly ConcurrentDictionary<string, TestRunSummary> _testRunSummariesDictionary = new();
    private List<TestRunSummary> _testRunSummaries = new();
    private readonly object _testRunSummariesLock = new();

    /// <summary>Creates a dedicated bounded report cache with the default policy.</summary>
    /// <param name="testRecordCache">Retained for compatibility; the host cache is no longer used for full reports.</param>
    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache)
        : this(testRecordCache, Options.Create(new BDTestReportOptions()))
    {
    }

    /// <summary>Creates a dedicated bounded report cache using the report server policy.</summary>
    /// <param name="testRecordCache">Retained for compatibility; full reports use a dedicated cache.</param>
    /// <param name="options">The report cache policy.</param>
    /// <param name="attachmentGate">Optional shared attachment gate; defaults to a new gate.</param>
    /// <param name="attachmentStore">Optional attachment storage for expiration cleanup.</param>
    /// <param name="logger">Optional logger for cleanup failures.</param>
    /// <param name="hasPersistentStorage">Whether another store retains reports when this cache evicts them.</param>
    public MemoryCacheBdTestDataStore(IMemoryCache testRecordCache, IOptions<BDTestReportOptions> options,
        ReportAttachmentGate? attachmentGate = null, IBDTestAttachmentStore? attachmentStore = null,
        ILogger<MemoryCacheBdTestDataStore>? logger = null, bool hasPersistentStorage = false)
        : this(testRecordCache, options, attachmentGate, attachmentStore, logger, hasPersistentStorage, null, null)
    {
    }

    // Separate clocks let tests expire the source while its projection remains cached.
    internal MemoryCacheBdTestDataStore(IMemoryCache testRecordCache, IOptions<BDTestReportOptions> options,
        ReportAttachmentGate? attachmentGate, IBDTestAttachmentStore? attachmentStore,
        ILogger<MemoryCacheBdTestDataStore>? logger, bool hasPersistentStorage,
        ISystemClock? reportClock, ISystemClock? pageClock)
    {
        _options = options.Value;
        _attachmentGate = attachmentGate ?? new ReportAttachmentGate();
        _attachmentStore = attachmentStore;
        _logger = logger;
        _hasPersistentStorage = hasPersistentStorage;
        _reportCacheCapacity = _options.ReportCacheSizeLimitBytes;
        _pageCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.ReportPageCacheSizeLimitBytes, TrackStatistics = true, Clock = pageClock });
        _testRecordCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _reportCacheCapacity,
            TrackStatistics = true,
            Clock = reportClock
        });
    }

    public Task<BDTestOutputModel?> GetTestData(string id, CancellationToken cancellationToken)
        => GetTestData(id, cancellationToken, refreshRetention: false);

    Task<BDTestOutputModel?> IMemoryCacheBdTestDataStore.GetTestDataAndRefreshRetention(string id,
        CancellationToken cancellationToken)
        => GetTestData(id, cancellationToken, refreshRetention: true);

    private async Task<BDTestOutputModel?> GetTestData(string id, CancellationToken cancellationToken, bool refreshRetention)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CachedReport? report;
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache;
            report = cache.Get<CachedReport>(id);
            if (refreshRetention && report != null) SetCacheEntry(cache, id, report);
        }
        var value = report?.Value;
        if (value is not byte[] bytes) return value as BDTestOutputModel;
        using var stream = new MemoryStream(bytes, writable: false);
        return await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken);
    }

    async Task<ReportPageModels?> IMemoryCacheBdTestDataStore.GetReportPagesAndRefreshRetention(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = new PageCacheKey(id, System.Globalization.CultureInfo.CurrentCulture.Name);
        CachedReport? source;
        lock (_reportCacheLock)
        {
            source = _testRecordCache.Get<CachedReport>(id);
            if (source == null) { RemovePages(id); return null; }
            SetCacheEntry(_testRecordCache, id, source);
            if (_pageCache.Get<CachedPages>(key) is { } cached && cached.Generation == source.Generation)
            {
                SetPages(key, cached);
                return cached.Pages;
            }
        }
        var model = source.Value as BDTestOutputModel;
        if (source.Value is byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            model = await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken);
        }
        if (model == null) return null;
        var pages = ReportPageModels.Create(model);
        long size;
        using (var counter = new ByteCountingStream())
        {
            JsonSerializer.Serialize(counter, pages, PageSizeOptions);
            size = counter.Length;
        }
        cancellationToken.ThrowIfCancellationRequested();
        lock (_reportCacheLock)
        {
            // Replacement/deletion while projecting must never repopulate stale data.
            if (!ReferenceEquals(_testRecordCache.Get<CachedReport>(id), source)) return null;
            if (size <= _options.ReportPageCacheSizeLimitBytes)
            {
                while (_pageCache.GetCurrentStatistics()!.CurrentEstimatedSize > _options.ReportPageCacheSizeLimitBytes - size)
                    _pageCache.Compact(Math.Min(1, Math.Max(0.25, 2.0 / _pageCache.Count)));
                SetPages(key, new CachedPages(source.Generation, pages, size));
            }
        }
        return pages;
    }

    private void SetPages(PageCacheKey key, CachedPages pages) => _pageCache.Set(key, pages,
        new MemoryCacheEntryOptions { Size = pages.Size, AbsoluteExpirationRelativeToNow = _options.ReportCacheRetention });

    private readonly record struct PageCacheKey(string ReportId, string Culture);

    // Enumerate only the bounded page cache: no unbounded report-to-variant index is retained.
    private void RemovePages(string id, Guid? generation = null)
    {
        foreach (var key in _pageCache.Keys.OfType<PageCacheKey>().Where(key => key.ReportId == id))
            if (generation == null || _pageCache.Get<CachedPages>(key)?.Generation == generation)
                _pageCache.Remove(key);
    }

    private sealed record CachedPages(Guid Generation, ReportPageModels Pages, long Size);

    public Task DeleteTestData(string id)
    {
        lock (_reportCacheLock)
        {
            _testRecordCache.Remove(id);
            RemovePages(id);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        return Task.FromResult(Volatile.Read(ref _testRunSummaries).AsEnumerable());
    }

    public Task StoreTestData(string id, BDTestOutputModel? data)
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

    private void CacheReport(string id, object? data, long size, bool allowOversizedBypass = true)
    {
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache;
            if (size > _reportCacheCapacity)
            {
                if (!allowOversizedBypass || !_hasPersistentStorage)
                {
                    throw new ReportTooLargeForCacheException();
                }

                // Persistent storage remains authoritative for reports that do not fit in memory.
                cache.Remove(id);
                RemovePages(id);
                return;
            }

            cache.Remove(id);
            RemovePages(id);
            while ((cache.GetCurrentStatistics() ?? throw new InvalidOperationException("Report cache statistics must be enabled.")).CurrentEstimatedSize > _reportCacheCapacity - size)
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
            lock (_reportCacheLock)
            {
                if (_disposed) return;
                if (value is CachedReport evicted && !ReferenceEquals(_testRecordCache.Get<CachedReport>(key), evicted))
                    RemovePages((string)key, evicted.Generation);
            }
            if (reason is EvictionReason.Expired or EvictionReason.Capacity
                && !_hasPersistentStorage && _attachmentStore != null
                && AttachmentStorage.IsValidId((string)key))
                _ = DeleteExpiredAttachments((string)key);
        }));
    }

    private async Task DeleteExpiredAttachments(string reportId)
    {
        try
        {
            using var lease = await _attachmentGate.EnterAsync(reportId);
            lock (_reportCacheLock)
            {
                if (_testRecordCache.TryGetValue(reportId, out _)) return;
            }
            if (_attachmentStore != null) await _attachmentStore.DeleteReportAsync(reportId);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Failed to remove attachments for expired report {ReportId}", reportId);
        }
    }

    private sealed record CachedReport(object? Value, long Size)
    {
        public Guid Generation { get; } = Guid.NewGuid();
    }

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
        lock (_reportCacheLock)
        {
            _disposed = true;
            _testRecordCache.Dispose();
            _pageCache.Dispose();
        }
    }

    public Task StoreTestRunRecord(TestRunSummary testRunSummary)
    {
        lock (_testRunSummariesLock)
        {
            _testRunSummariesDictionary.TryAdd(testRunSummary.RecordId ?? throw new ArgumentException("A run record ID is required.", nameof(testRunSummary)), testRunSummary);

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

    public async Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? value;
        lock (_reportCacheLock)
        {
            var cache = _testRecordCache;
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
            var exists = _testRecordCache.TryGetValue(reportId, out _);
            _testRecordCache.Remove(reportId);
            RemovePages(reportId);
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
            _testRunSummariesDictionary[entry.RecordId ?? throw new ArgumentException("A run record ID is required.", nameof(entry))] = entry.ToTestRunSummary();
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
