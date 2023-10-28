using BDTest.Maps;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IDataRepository
{
    Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken);
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
    Task DeleteReport(string id);
}