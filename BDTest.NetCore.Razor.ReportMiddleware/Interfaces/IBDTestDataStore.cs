using System.Collections.Generic;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IBDTestDataStore
    {
        Task<BDTestOutputModel> GetTestData(string id);
        Task StoreTestData(string id, BDTestOutputModel data);
        Task DeleteTestData(string id);
        
        Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
        Task StoreTestRunRecord(TestRunSummary testRunSummary);
        Task DeleteTestRunRecord(string id);
    }
}