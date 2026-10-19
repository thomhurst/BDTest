using System.Runtime.ExceptionServices;
using System.Text.Json;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.Logging;

namespace BDTest.Razor.Reports.Implementations;

internal class DataRepository : IDataRepository
{
    private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
    private readonly IReportStore? _reportStore;
    private readonly IRunIndexStore? _runIndexStore;
    private readonly SemaphoreSlim _historyLock = new(1, 1);
    private bool _historyLoaded;
    private readonly IBDTestAttachmentStore? _attachmentStore;
    private readonly ReportAttachmentGate _attachmentGate;
    private readonly ILogger<DataRepository>? _logger;

    public DataRepository(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, ReportAttachmentGate attachmentGate,
        IBDTestDataStore? dataStore = null, IReportStore? reportStore = null, IRunIndexStore? runIndexStore = null,
        IBDTestAttachmentStore? attachmentStore = null, ILogger<DataRepository>? logger = null)
    {
        _attachmentGate = attachmentGate;
        _logger = logger;
        _attachmentStore = attachmentStore;
        _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
        if ((reportStore == null) != (runIndexStore == null))
            throw new ArgumentException("Register both IReportStore and IRunIndexStore.");
        var legacy = dataStore == null ? null : new LegacyDataStoreAdapter(dataStore);
        _reportStore = reportStore ?? dataStore as IReportStore ?? legacy;
        _runIndexStore = runIndexStore ?? dataStore as IRunIndexStore ?? legacy;
    }

    public async Task<ReportPageModels?> GetReportPages(string reportId, CancellationToken cancellationToken)
    {
        using var lease = await _attachmentGate.EnterAsync(reportId, cancellationToken);
        var pages = await _memoryCacheBdTestDataStore.GetReportPagesAndRefreshRetention(reportId, cancellationToken);
        if (pages != null) return pages;
        var model = await GetData(reportId, cancellationToken, cacheResult: true);
        if (model == null) return null;
        return await _memoryCacheBdTestDataStore.GetReportPagesAndRefreshRetention(reportId, cancellationToken)
            ?? ReportPageModels.Create(model); // Oversized persistent reports bypass both caches.
    }

    public async Task<BDTestOutputModel?> GetData(string reportId, CancellationToken cancellationToken)
    {
        // A read that fills the cache must finish before deletion invalidates it.
        using var lease = await _attachmentGate.EnterAsync(reportId, cancellationToken);
        return await GetData(reportId, cancellationToken, cacheResult: true);
    }

    public Task<BDTestOutputModel?> GetDataForBulkRead(string reportId, CancellationToken cancellationToken)
        => GetData(reportId, cancellationToken, cacheResult: false);

    private async Task<BDTestOutputModel?> GetData(string reportId, CancellationToken cancellationToken, bool cacheResult)
    {
        var model = cacheResult
            ? await _memoryCacheBdTestDataStore.GetTestDataAndRefreshRetention(reportId, cancellationToken)
            : await _memoryCacheBdTestDataStore.GetTestData(reportId, cancellationToken);
        if (model != null)
        {
            NormalizeReport(model);
            return model;
        }
        if (_reportStore != null)
        {
            await using var stream = await _reportStore.OpenReadAsync(reportId, cancellationToken);
            if (stream != null) model = await BDTestJsonHelper.DeserializeAsync(stream, cancellationToken);
        }
        if (model == null) return null;
        NormalizeReport(model);
        cancellationToken.ThrowIfCancellationRequested();
        if (cacheResult) await _memoryCacheBdTestDataStore.StoreTestData(reportId, model);
        return model;
    }

    public Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Persistent bytes are authoritative, including their original JSON formatting.
        return (_reportStore ?? _memoryCacheBdTestDataStore).OpenReadAsync(reportId, cancellationToken);
    }

    private async Task EnsureHistoryLoaded(CancellationToken cancellationToken)
    {
        if (_historyLoaded || _runIndexStore == null) return;
        // Callers hold _historyLock so deletion cannot race a persisted snapshot.
        string? continuation = null;
        do
        {
            var page = await _runIndexStore.ListAsync(new RunIndexQuery
            {
                PageSize = 500, ContinuationToken = continuation
            }, cancellationToken);
            foreach (var entry in page.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Add-only legacy cache operation preserves uploads made during this load.
                await _memoryCacheBdTestDataStore.StoreTestRunRecord(entry.ToTestRunSummary());
            }
            continuation = page.ContinuationToken;
        } while (continuation != null);
        _historyLoaded = true;
    }

    public async Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
    {
        await _historyLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureHistoryLoaded(cancellationToken);
            return await _memoryCacheBdTestDataStore.ListAsync(query, cancellationToken);
        }
        finally { _historyLock.Release(); }
    }

    public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default)
    {
        await _historyLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureHistoryLoaded(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await _memoryCacheBdTestDataStore.GetAllTestRunRecords();
        }
        finally { _historyLock.Release(); }
    }

    public async Task StoreData(string reportId, BDTestOutputModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NormalizeReport(model);
        if (await _memoryCacheBdTestDataStore.GetTestData(reportId, cancellationToken) != null) return;
        if (_reportStore != null)
        {
            await using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, model, BDTestJsonContext.Default.BDTestOutputModel, cancellationToken);
            stream.Position = 0;
            await _reportStore.WriteAsync(reportId, stream, cancellationToken);
            await (_runIndexStore ?? throw new InvalidOperationException("A report store requires a run index store.")).UpsertAsync(model.GetOverview().ToRunIndexEntry(), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await _memoryCacheBdTestDataStore.StoreTestData(reportId, model);
        await _memoryCacheBdTestDataStore.StoreTestRunRecord(model.GetOverview());
    }

    private static void NormalizeReport(BDTestOutputModel model)
    {
        model.Scenarios ??= new();
        model.NotRun ??= new();
        model.TestTimer ??= new TestTimer { TestsStartedAt = default, TestsFinishedAt = default };
    }

    public async Task DeleteReport(string reportId, CancellationToken cancellationToken = default)
    {
        using var lease = await _attachmentGate.EnterAsync(reportId, cancellationToken);
        await DeleteReportAndAttachments(reportId, cancellationToken);
    }

    private async Task DeleteReportAndAttachments(string reportId, CancellationToken cancellationToken)
    {
        await _historyLock.WaitAsync(cancellationToken);
        try
        {
            Exception? failure = null;
            var deleted = false;
            try
            {
                // An index failure must leave report data intact. Orphan data can be retried safely.
                if (_runIndexStore != null)
                    deleted |= await _runIndexStore.DeleteAsync(reportId, cancellationToken);
                if (_attachmentStore != null && AttachmentStorage.IsValidId(reportId))
                    await _attachmentStore.DeleteReportAsync(reportId, cancellationToken);
                if (_reportStore != null)
                    deleted |= await _reportStore.DeleteAsync(reportId, cancellationToken);
            }
            catch (Exception exception) { failure = exception; }

            // Always attempt both invalidations, even after cancellation. Keep the first failure.
            try
            {
                deleted |= await ((IRunIndexStore)_memoryCacheBdTestDataStore).DeleteAsync(reportId, CancellationToken.None);
            }
            catch (Exception exception) { failure ??= exception; }
            try
            {
                deleted |= await ((IReportStore)_memoryCacheBdTestDataStore).DeleteAsync(reportId, CancellationToken.None);
            }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            if (!deleted)
                _logger?.LogDebug("Report {ReportId} did not exist during deletion.",
                    reportId.Replace("\r", "\\r").Replace("\n", "\\n"));
        }
        finally { _historyLock.Release(); }
    }
}
