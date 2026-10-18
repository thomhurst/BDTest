using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Implementations;

internal class DataRepository : IDataRepository
{
    private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
    private readonly IBDTestDataStore _customDatastore;
    private readonly SemaphoreSlim _historyLock = new(1, 1);
    private bool _historyLoaded;

    public DataRepository(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, BDTestReportServerOptions options)
    {
        _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
        _customDatastore = options.DataStore;
    }

    public Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken)
        => GetData(id, cancellationToken, cacheResult: true);

    public Task<BDTestOutputModel> GetDataForBulkRead(string id, CancellationToken cancellationToken)
        => GetData(id, cancellationToken, cacheResult: false);

    private async Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken, bool cacheResult)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Check if it's already in-memory
        var model = await _memoryCacheBdTestDataStore.GetTestData(id, cancellationToken);

        if (model == null && _customDatastore != null)
        {
            // Search the backup persistent storage
            model = await _customDatastore.GetTestData(id, cancellationToken);
        }

        if (model == null)
        {
            return null;
        }

        NormalizeReport(model);

        if (cacheResult)
        {
            // Single-report reads re-cache it to extend the time. Bulk reads must not retain full reports.
            await _memoryCacheBdTestDataStore.StoreTestData(id, model);
        }

        return model;
    }

    public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
    {
        if (_customDatastore == null)
        {
            return await _memoryCacheBdTestDataStore.GetAllTestRunRecords();
        }

        await _historyLock.WaitAsync();
        try
        {
            if (!_historyLoaded)
            {
                // A cached upload does not mean persisted history has been loaded.
                var records = (await _customDatastore.GetAllTestRunRecords() ?? Array.Empty<TestRunSummary>()).ToList();
                foreach (var record in records)
                {
                    // The cache deduplicates by RecordId and preserves uploads made during this load.
                    await _memoryCacheBdTestDataStore.StoreTestRunRecord(record);
                }

                _historyLoaded = true;
            }

            return await _memoryCacheBdTestDataStore.GetAllTestRunRecords();
        }
        finally
        {
            _historyLock.Release();
        }
    }

    public async Task StoreData(BDTestOutputModel bdTestOutputModel, string id)
    {
        NormalizeReport(bdTestOutputModel);

        if (await _memoryCacheBdTestDataStore.GetTestData(id, default) == null)
        {
            // Cache within the configured capacity and retention policy.
            await _memoryCacheBdTestDataStore.StoreTestData(id, bdTestOutputModel);
            await _memoryCacheBdTestDataStore.StoreTestRunRecord(bdTestOutputModel.GetOverview());

            if (_customDatastore != null)
            {
                // Save to persistent storage if it's configured!
                await _customDatastore.StoreTestData(id, bdTestOutputModel);
                await _customDatastore.StoreTestRunRecord(bdTestOutputModel.GetOverview());
            }
        }
    }

    private static void NormalizeReport(BDTestOutputModel model)
    {
        model.Scenarios ??= new();
        model.NotRun ??= new();
        // Missing timing data must not become the report server's process/startup time.
        model.TestTimer ??= new TestTimer { TestsStartedAt = default, TestsFinishedAt = default };
    }

    public async Task DeleteReport(string id)
    {
        // Do not let an in-flight history snapshot reintroduce a deleted record.
        await _historyLock.WaitAsync();
        try
        {
            if (_customDatastore != null)
            {
                await _customDatastore.DeleteTestData(id);
                await _customDatastore.DeleteTestRunRecord(id);
            }

            await _memoryCacheBdTestDataStore.DeleteTestData(id);
            await _memoryCacheBdTestDataStore.DeleteTestRunRecord(id);
        }
        finally
        {
            _historyLock.Release();
        }
    }
}
