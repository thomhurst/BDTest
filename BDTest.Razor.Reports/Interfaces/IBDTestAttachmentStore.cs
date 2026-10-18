#nullable enable

namespace BDTest.Razor.Reports.Interfaces;

/// <summary>Stores content under a report and attachment ID, independently of report JSON storage.</summary>
public interface IBDTestAttachmentStore
{
    /// <summary>Returns null for missing content. The caller owns the returned stream.</summary>
    Task<Stream?> OpenReadAsync(string reportId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces content after reading exactly length bytes. Leaves input open on every outcome.</summary>
    /// <remarks>A failed write must not publish partial content or replace previously complete content.</remarks>
    Task WriteAsync(string reportId, string attachmentId, Stream content, long length, CancellationToken cancellationToken = default);

    /// <summary>Deletes all content for exactly one report. Missing content is a successful no-op.</summary>
    Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default);
}
