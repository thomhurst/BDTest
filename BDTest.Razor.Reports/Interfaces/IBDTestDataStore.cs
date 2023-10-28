using BDTest.Maps;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestDataStore
{
    Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken);
    Task StoreTestData(string id, BDTestOutputModel data);
    Task DeleteTestData(string id);
        
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreTestRunRecord(TestRunSummary testRunSummary);
    Task DeleteTestRunRecord(string id);
}