#nullable enable
namespace BDTest.Razor.Reports.Storage;

/// <summary>Moves UTF-8 report JSON without interpreting it.</summary>
public interface IReportStore
{
    /// <summary>Returns null when missing. The caller owns and must dispose the returned stream.</summary>
    Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken);
    /// <summary>Writes from the current position, replacing any existing report. Does not dispose the caller's stream.</summary>
    Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken);
    /// <summary>Returns true if a report was deleted, false if it did not exist.</summary>
    Task<bool> DeleteAsync(string reportId, CancellationToken cancellationToken);
}
