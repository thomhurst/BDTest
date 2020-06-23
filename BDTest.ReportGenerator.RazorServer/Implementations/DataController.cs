using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Extensions;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using BDTest.ReportGenerator.RazorServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.ReportGenerator.RazorServer.Implementations
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

        [MethodImpl(MethodImplOptions.Synchronized)]
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
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public async Task<IEnumerable<TestRunOverview>> GetRunsBetweenTimes(DateTime start, DateTime end)
        {
            return await _methodLock.RunAndReturnAsync(async () =>
            {
                // Check if it's already in-memory
                var model = await _memoryCacheBdTestDataStore.GetTestRunRecordsBetweenDateTimes(start, end) ??
                            Array.Empty<TestRunOverview>();

                if (!model.Any() && _customDatastore != null)
                {
                    // Search the backup persistent storage
                    model = await _customDatastore.GetTestRunRecordsBetweenDateTimes(start, end);

                    foreach (var testRunRecord in model ?? Array.Empty<TestRunOverview>())
                    {
                        await _memoryCacheBdTestDataStore.StoreTestRunRecord(testRunRecord);
                    }
                }

                return model;
            }, CancellationToken.None);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
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