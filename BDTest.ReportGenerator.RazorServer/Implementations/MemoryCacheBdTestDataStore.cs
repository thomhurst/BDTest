using System;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.ReportGenerator.RazorServer.Implementations
{
    public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore
    {
        private readonly IMemoryCache _cache;

        public MemoryCacheBdTestDataStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<BDTestOutputModel> GetDataFromStore(string id)
        {
            if (_cache.TryGetValue<BDTestOutputModel>(id, out var model))
            {
                return Task.FromResult(model);
            }

            return Task.FromResult(null as BDTestOutputModel);
        }

        public Task StoreData(string id, BDTestOutputModel data)
        {
            _cache.Set(id, data, TimeSpan.FromHours(3));
            return Task.CompletedTask;
        }
    }
}