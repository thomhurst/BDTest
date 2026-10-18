using BDTest.Maps;

namespace BDTest.Razor.Reports.Interfaces;

internal interface IMemoryCacheBdTestDataStore : IBDTestDataStore
{
    Task<BDTestOutputModel> GetTestDataAndRefreshRetention(string id, CancellationToken cancellationToken);
}
