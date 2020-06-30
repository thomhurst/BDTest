using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations
{
    internal class DataController : IDataController
    {
        private readonly IMemoryCacheBdTestDataStore _memoryCacheBdTestDataStore;
        private readonly IBDTestDataStore _customDatastore;
        private readonly SemaphoreSlim _methodLock = new SemaphoreSlim(1, 1);
        
        public DataController(IMemoryCacheBdTestDataStore memoryCacheBdTestDataStore, IServiceProvider serviceProvider)
        {
            _memoryCacheBdTestDataStore = memoryCacheBdTestDataStore;
            _customDatastore = serviceProvider.GetService<IBDTestDataStore>();
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
            return await _methodLock.RunAndReturnAsync(async () =>
            {
                // Check if it's already in-memory
                var model = await _memoryCacheBdTestDataStore.GetAllTestRunRecords() ??
                            Array.Empty<TestRunSummary>();

                if (!model.Any() && _customDatastore != null)
                {
                    // Search the backup persistent storage
                    model = await _customDatastore.GetAllTestRunRecords();

                    foreach (var testRunRecord in model ?? Array.Empty<TestRunSummary>())
                    {
                        await _memoryCacheBdTestDataStore.StoreTestRunRecord(testRunRecord);
                    }
                }

                return model;
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
    }
}