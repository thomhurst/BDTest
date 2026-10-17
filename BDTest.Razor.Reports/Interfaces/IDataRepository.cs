using BDTest.Maps;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IDataRepository
{
    Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a report for a bulk comparison. The built-in repository does not populate or refresh its cache.
    /// Custom repositories can override this method to bypass their own cache writes; the default preserves
    /// compatibility with existing implementations by delegating to <see cref="GetData"/>.
    /// </summary>
    Task<BDTestOutputModel> GetDataForBulkRead(string id, CancellationToken cancellationToken)
        => GetData(id, cancellationToken);

    Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords();
    Task StoreData(BDTestOutputModel bdTestOutputModel, string id);
    Task DeleteReport(string id);
}