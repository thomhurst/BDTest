using BDTest.Maps;

namespace BDTest.Razor.Reports.Interfaces;

internal interface IMemoryCacheBdTestDataStore : IBDTestDataStore, BDTest.Razor.Reports.Storage.IReportStore, BDTest.Razor.Reports.Storage.IRunIndexStore
{
    Task<BDTestOutputModel> GetTestDataAndRefreshRetention(string id, CancellationToken cancellationToken);
}
