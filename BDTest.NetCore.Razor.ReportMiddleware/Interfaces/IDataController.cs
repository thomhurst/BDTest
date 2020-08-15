using System.Collections.Generic;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IDataController
    {
        Task<BDTestOutputModel> GetData(string id);
        Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
        Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
        Task DeleteReport(string id);
    }
}