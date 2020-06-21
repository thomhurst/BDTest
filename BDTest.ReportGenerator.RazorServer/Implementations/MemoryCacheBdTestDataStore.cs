using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Interfaces;
using BDTest.ReportGenerator.RazorServer.Models;
using BDTest.Test;
using Microsoft.Extensions.Caching.Memory;

namespace BDTest.ReportGenerator.RazorServer.Implementations
{
    public class MemoryCacheBdTestDataStore : IMemoryCacheBdTestDataStore
    {
        private const string RecordDateTimeModelsKey = "RecordDateTimeModels";
        private readonly IMemoryCache _cache;

        public MemoryCacheBdTestDataStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<BDTestOutputModel> GetRecord(string id)
        {
            if (_cache.TryGetValue<BDTestOutputModel>(id, out var model))
            {
                return Task.FromResult(model);
            }

            return Task.FromResult(null as BDTestOutputModel);
        }

        public Task<RecordDateTimeModel[]> GetRecordsBetweenDateTimes(DateTime start, DateTime end)
        {
            var recordDateTimeModels = new List<RecordDateTimeModel>();
            
            if(_cache.TryGetValue(RecordDateTimeModelsKey, out var recordDateTimeModelsAsObject))
            {
                recordDateTimeModels.AddRange(recordDateTimeModelsAsObject as IEnumerable<RecordDateTimeModel> ?? Array.Empty<RecordDateTimeModel>());
            }

            var recordsInDateRange = recordDateTimeModels.Where(model => model.DateTime > start && model.DateTime < end);
            
            return Task.FromResult(recordsInDateRange.ToArray());
        }

        public Task StoreRecord(string id, BDTestOutputModel data)
        {
            _cache.Set(id, data, TimeSpan.FromHours(3));
            return Task.CompletedTask;
        }

        public Task StoreRecordIdAndDateTime(string id, DateTime dateTime, Status status)
        {
            var recordDateTimeModels = new List<RecordDateTimeModel>();
            
            if(_cache.TryGetValue(RecordDateTimeModelsKey, out var recordDateTimeModelsAsObject))
            {
                recordDateTimeModels.AddRange(recordDateTimeModelsAsObject as IEnumerable<RecordDateTimeModel> ?? Array.Empty<RecordDateTimeModel>());
            }
            
            recordDateTimeModels.Add(new RecordDateTimeModel(id, dateTime, status));

            _cache.Set(RecordDateTimeModelsKey, recordDateTimeModels);
            
            return Task.CompletedTask;
        }
    }
}