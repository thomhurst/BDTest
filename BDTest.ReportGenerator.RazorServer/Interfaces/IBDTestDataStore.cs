using System;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Models;
using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IBDTestDataStore
    {
        public Task<BDTestOutputModel> GetRecord(string id);
        public Task<RecordDateTimeModel[]> GetRecordsBetweenDateTimes(DateTime start, DateTime end);
        public Task StoreRecord(string id, BDTestOutputModel data);
        public Task StoreRecordIdAndDateTime(string id, DateTime dateTime, Status status);
    }
}