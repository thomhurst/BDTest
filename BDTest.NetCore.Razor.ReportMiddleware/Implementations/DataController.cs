using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations
{
    internal class DataController : IDataController, IDisposable
    {
        private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
        private readonly IBDTestDataStore _customDatastore;
        private readonly SemaphoreSlim _methodLock = new SemaphoreSlim(1, 1);
        
        public DataController(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, BDTestReportServerOptions options)
        {
            _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
            _customDatastore = options.DataStore;
            
            // Get all records on startup
            Task.Run(GetAllTestRunRecords);
        }
        
        public async Task<BDTestOutputModel> GetData(string id)
        {
            return await _methodLock.RunAndReturnAsync(async () =>
            {
                // Check if it's already in-memory
                var model = await _memoryCacheBdTestDataStore.GetTestData(id);

                if (model == null && _customDatastore != null)
                {
                    // Search the backup persistent storage
                    model = await _customDatastore.GetTestData(id);
                }

                if (model == null)
                {
                    return null;
                }

                // Re-cache it to extend the time
                await _memoryCacheBdTestDataStore.StoreTestData(id, model);

                return model;
            }, CancellationToken.None);
        }
        
        public async Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            return await _methodLock.RunAndReturnAsync<IEnumerable<TestRunSummary>>(async () =>
            {
                // Check if it's already in-memory
                var memoryCachedModels = (await _memoryCacheBdTestDataStore.GetAllTestRunRecords()).ToList();

                if (memoryCachedModels.Any() || _customDatastore == null)
                {
                    return memoryCachedModels;
                }
                
                // Search the backup persistent storage
                var dataStoredModels = await _customDatastore.GetAllTestRunRecords() ?? Array.Empty<TestRunSummary>();
                var dataStoredModelsAsList = dataStoredModels.ToList();
                
                foreach (var testRunRecord in dataStoredModelsAsList)
                {
                    await _memoryCacheBdTestDataStore.StoreTestRunRecord(testRunRecord);
                }

                return dataStoredModelsAsList.OrderByDescending(record => record.StartedAtDateTime);
            }, CancellationToken.None);
        }
        
        public async Task StoreData(BDTestOutputModel bdTestOutputModel, string id)
        {
            await _methodLock.RunAsync(async () =>
            {
                if (await _memoryCacheBdTestDataStore.GetTestData(id) == null)
                {
                    // Save to in-memory cache for 8 hours for quick fetching
                    await _memoryCacheBdTestDataStore.StoreTestData(id, bdTestOutputModel);
                    await _memoryCacheBdTestDataStore.StoreTestRunRecord(bdTestOutputModel.GetOverview());

                    if (_customDatastore != null)
                    {
                        // Save to persistent storage if it's configured!
                        await _customDatastore.StoreTestData(id, bdTestOutputModel);
                        await _customDatastore.StoreTestRunRecord(bdTestOutputModel.GetOverview());
                    }
                }
            }, CancellationToken.None);
        }

        public async Task DeleteReport(string id)
        {
            await _methodLock.RunAsync(async () =>
            {
                if (_customDatastore != null)
                {
                    // Save to persistent storage if it's configured!
                    await _customDatastore.DeleteTestData(id);
                    await _customDatastore.DeleteTestRunRecord(id);
                }

                await _memoryCacheBdTestDataStore.DeleteTestData(id);
                await _memoryCacheBdTestDataStore.DeleteTestRunRecord(id);
            }, CancellationToken.None);
        }

        public void Dispose()
        {
            _methodLock?.Dispose();
        }
    }
}