namespace BDTest.Razor.Reports.Storage;

/// <summary>Persists and pages run metadata independently of report JSON and attachments.</summary>
/// <remarks>Index and report operations are separate, not a transaction. Implementations must observe cancellation.</remarks>
public interface IRunIndexStore
{
    /// <summary>Lists metadata without loading report payloads, newest start time first, then ordinal report ID.</summary>
    /// <param name="query">Page size (1–500) and optional exact, case-sensitive ordinal filters. Reuse the filters when continuing.</param>
    /// <param name="cancellationToken">Cancels listing and any storage I/O.</param>
    /// <returns>A page with an opaque continuation token, or a null token at the end. An empty page can still have a continuation token.</returns>
    Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken);
    /// <summary>Inserts metadata or replaces the entry with the same <see cref="RunIndexEntry.RecordId"/>.</summary>
    /// <remarks>Does not write report bytes or attachments.</remarks>
    Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken);
    /// <summary>Deletes only index metadata. Returns true if an entry was deleted, false if it did not exist.</summary>
    /// <remarks>Must be safe to retry after a partial report deletion. Does not delete report bytes or attachments.</remarks>
    Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken);
}
