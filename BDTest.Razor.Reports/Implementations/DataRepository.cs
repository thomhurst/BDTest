using System.Text.Json;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Implementations;

internal class DataRepository : IDataRepository
{
    private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
    private readonly IReportStore _reportStore;
    private readonly IRunIndexStore _runIndexStore;
    private readonly SemaphoreSlim _historyLock = new(1, 1);
    private bool _historyLoaded;
    private readonly BDTestReportServerOptions _options;

    public DataRepository(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, BDTestReportServerOptions options)
    {
        _options = options;
        _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
        if ((options.ReportStore == null) != (options.RunIndexStore == null))
            throw new ArgumentException("Configure both ReportStore and RunIndexStore.", nameof(options));
        var legacy = options.DataStore == null ? null : new LegacyDataStoreAdapter(options.DataStore);
        _reportStore = options.ReportStore ?? options.DataStore as IReportStore ?? legacy;
        _runIndexStore = options.RunIndexStore ?? options.DataStore as IRunIndexStore ?? legacy;
    }

    public Task<BDTestOutputModel> GetData(string reportId, CancellationToken cancellationToken)
        => GetData(reportId, cancellationToken, cacheResult: true);

    public Task<BDTestOutputModel> GetDataForBulkRead(string reportId, CancellationToken cancellationToken)
        => GetData(reportId, cancellationToken, cacheResult: false);

    private async Task<BDTestOutputModel> GetData(string reportId, CancellationToken cancellationToken, bool cacheResult)
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

    public Task<Stream> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Persistent bytes are authoritative, including their original JSON formatting.
        return (_reportStore ?? _memoryCacheBdTestDataStore).OpenReadAsync(reportId, cancellationToken);
    }

    private async Task EnsureHistoryLoaded(CancellationToken cancellationToken)
    {
        if (_historyLoaded || _runIndexStore == null) return;
        // Callers hold _historyLock so deletion cannot race a persisted snapshot.
        string continuation = null;
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
            await _runIndexStore.UpsertAsync(model.GetOverview().ToRunIndexEntry(), cancellationToken);
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
        using var lease = await _options.AttachmentGate.EnterAsync(reportId, cancellationToken);
        await DeleteReportAndAttachments(reportId, cancellationToken);
    }

    private async Task DeleteReportAndAttachments(string reportId, CancellationToken cancellationToken)
    {
        await _historyLock.WaitAsync(cancellationToken);
        try
        {
            if (_options.AttachmentStore != null && AttachmentStorage.IsValidId(reportId))
                await _options.AttachmentStore.DeleteReportAsync(reportId, cancellationToken);
            if (_reportStore != null)
            {
                await _reportStore.DeleteAsync(reportId, cancellationToken);
                await _runIndexStore.DeleteAsync(reportId, cancellationToken);
            }
            await ((IReportStore)_memoryCacheBdTestDataStore).DeleteAsync(reportId, cancellationToken);
            await ((IRunIndexStore)_memoryCacheBdTestDataStore).DeleteAsync(reportId, cancellationToken);
        }
        finally { _historyLock.Release(); }
    }
}
