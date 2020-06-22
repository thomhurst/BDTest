using System;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Models;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IBDTestDataStore
    {
        public Task<BDTestOutputModel> GetTestData(string id);
        public Task<TestRunOverview[]> GetTestRunRecordsBetweenDateTimes(DateTime start, DateTime end);
        public Task StoreTestData(string id, BDTestOutputModel data);
        public Task StoreTestRunRecord(TestRunOverview testRunOverview);
    }
}