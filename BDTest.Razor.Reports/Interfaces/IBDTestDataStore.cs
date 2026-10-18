using BDTest.Maps;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestDataStore
{
    /// <summary>Returns null when the report is missing or stored as a null model.</summary>
    Task<BDTestOutputModel?> GetTestData(string id, CancellationToken cancellationToken);
    /// <summary>Stores a model; legacy JSON null roots are passed through as null.</summary>
    Task StoreTestData(string id, BDTestOutputModel? data);
    Task DeleteTestData(string id);
        
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreTestRunRecord(TestRunSummary testRunSummary);
    Task DeleteTestRunRecord(string id);
}