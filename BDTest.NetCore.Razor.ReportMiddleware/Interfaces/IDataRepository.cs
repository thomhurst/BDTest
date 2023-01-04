using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

public interface IDataRepository
{
    Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken);
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
    Task DeleteReport(string id);
}