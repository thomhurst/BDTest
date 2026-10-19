using BDTest.Contracts;

namespace BDTest.Rendering;

/// <summary>Host-owned routing and presentation policy. Captured attachment URIs are never used.</summary>
public sealed class ReportRenderOptions
{
    /// <summary>Resolves an attachment to an authorized, application-relative download route, or null.</summary>
    public Func<AttachmentInfo, string?>? AttachmentUrl { get; set; }

    /// <summary>Use the stricter hosted custom-HTML sandbox (no popup or local-file permissions).</summary>
    public bool Hosted { get; set; }

    /// <summary>Omit custom HTML in a side panel; the full scenario page still contains it.</summary>
    public bool OmitCustomHtml { get; set; }
}
