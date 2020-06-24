using System;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IBDTestDataStore
    {
        Task<BDTestOutputModel> GetTestData(string id);
        Task<TestRunOverview[]> GetTestRunRecordsBetweenDateTimes(DateTime start, DateTime end);
        Task StoreTestData(string id, BDTestOutputModel data);
        Task StoreTestRunRecord(TestRunOverview testRunOverview);
    }
}