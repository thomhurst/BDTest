namespace BDTest.Razor.Reports.Storage;

public interface IRunIndexStore
{
    Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken);
    Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken);
}
