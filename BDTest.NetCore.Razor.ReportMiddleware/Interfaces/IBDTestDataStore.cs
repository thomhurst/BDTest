using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IBDTestDataStore
    {
        Task<BDTestOutputModel> GetTestData(string id);
        Task<TestRunSummary[]> GetAllTestRunRecords();
        Task StoreTestData(string id, BDTestOutputModel data);
        Task StoreTestRunRecord(TestRunSummary testRunSummary);
    }
}