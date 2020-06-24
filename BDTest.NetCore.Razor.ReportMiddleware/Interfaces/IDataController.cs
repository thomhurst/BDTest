using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IDataController
    {
        Task<BDTestOutputModel> GetData(string id);
        Task<IEnumerable<TestRunOverview>> GetRunsBetweenTimes(DateTime start, DateTime end);
        Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
    }
}