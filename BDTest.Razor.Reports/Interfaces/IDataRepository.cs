using BDTest.Contracts;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Interfaces;

public interface IDataRepository
{
    /// <summary>Compact report pages. Custom repositories may override this uncached fallback.</summary>
    async Task<ReportPageModels?> GetReportPages(string reportId, CancellationToken cancellationToken)
    {
        var report = await GetData(reportId, cancellationToken);
        return report == null ? null : ReportPageModels.Create(report);
    }

    Task<BDTestReport?> GetData(string reportId, CancellationToken cancellationToken);
    /// <summary>Reads without adding or refreshing full reports in the cache.</summary>
    Task<BDTestReport?> GetDataForBulkRead(string reportId, CancellationToken cancellationToken);
    /// <summary>The caller owns the returned stream, or receives null for a missing report.</summary>
    Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken);
    Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken);
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default);
    Task StoreData(string reportId, BDTestReport model, CancellationToken cancellationToken = default);
    Task DeleteReport(string reportId, CancellationToken cancellationToken = default);
}
