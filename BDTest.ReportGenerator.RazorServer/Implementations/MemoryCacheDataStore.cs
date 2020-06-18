using System.Threading.Tasks;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.ReportGenerator.RazorServer.Implementations
{
    public class MemoryCacheDataStore : IMemoryCacheDataStore
    {
        private readonly IMemoryCache _cache;

        public MemoryCacheDataStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<string> GetDataFromStore(string id)
        {
            if (_cache.TryGetValue(id, out var model))
            {
                return Task.FromResult(model.ToString());
            }

            return Task.FromResult(null as string);
        }

        public Task StoreData(string id, string data)
        {
            _cache.Set(id, data);
            return Task.CompletedTask;
        }
    }
}