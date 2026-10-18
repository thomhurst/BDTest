#nullable enable
using BDTest.Maps;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Interfaces;

public interface IDataRepository
{
    Task<BDTestOutputModel?> GetData(string reportId, CancellationToken cancellationToken);
    /// <summary>Reads without adding or refreshing full reports in the cache.</summary>
    Task<BDTestOutputModel?> GetDataForBulkRead(string reportId, CancellationToken cancellationToken);
    /// <summary>The caller owns the returned stream, or receives null for a missing report.</summary>
    Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken);
    Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken);
    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default);
    Task StoreData(string reportId, BDTestOutputModel model, CancellationToken cancellationToken = default);
    Task DeleteReport(string reportId, CancellationToken cancellationToken = default);
}
