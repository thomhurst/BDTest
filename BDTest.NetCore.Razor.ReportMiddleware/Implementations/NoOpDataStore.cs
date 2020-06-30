using System;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations
{
    public class NoOpDataStore : IBDTestDataStore
    {
        public Task<BDTestOutputModel> GetTestData(string id)
        {
            return Task.FromResult(null as BDTestOutputModel);
        }

        public Task<TestRunSummary[]> GetAllTestRunRecords()
        {
            return Task.FromResult(Array.Empty<TestRunSummary>());
        }

        public Task StoreTestData(string id, BDTestOutputModel data)
        {
            return Task.CompletedTask;
        }

        public Task StoreTestRunRecord(TestRunSummary testRunSummary)
        {
            return Task.CompletedTask;
        }
    }
}