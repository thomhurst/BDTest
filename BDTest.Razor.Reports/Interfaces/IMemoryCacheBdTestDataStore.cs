using BDTest.Maps;
using BDTest.Razor.Reports.Models.ViewModels;

namespace BDTest.Razor.Reports.Interfaces;

internal interface IMemoryCacheBdTestDataStore : IBDTestDataStore, BDTest.Razor.Reports.Storage.IReportStore, BDTest.Razor.Reports.Storage.IRunIndexStore
{
    Task<ReportPageModels?> GetReportPagesAndRefreshRetention(string id, CancellationToken cancellationToken);
    Task<BDTestOutputModel?> GetTestDataAndRefreshRetention(string id, CancellationToken cancellationToken);
}
