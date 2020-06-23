using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Models;

namespace BDTest.ReportGenerator.RazorServer.Interfaces
{
    public interface IDataController
    {
        public Task<BDTestOutputModel> GetData(string id);
        public Task<IEnumerable<TestRunOverview>> GetRunsBetweenTimes(DateTime start, DateTime end);

        public Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
    }
}