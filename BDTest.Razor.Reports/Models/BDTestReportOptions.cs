#nullable enable

namespace BDTest.Razor.Reports.Models;

/// <summary>Startup configuration for report caching, uploads, attachments, and response headers.</summary>
public class BDTestReportOptions
{
        /// <summary>Named ASP.NET Core policy required for all report reads. Unset denies access unless endpoint authorization or AllowAnonymousReports is configured.</summary>
        public string? ReportsAuthorizationPolicy { get; set; }
        /// <summary>Explicitly permits anonymous report reads when no read policy is configured. Does not bypass host authorization.</summary>
        public bool AllowAnonymousReports { get; set; }
        /// <summary>Named ASP.NET Core policy required to delete reports. Unset disables deletion.</summary>
        public string? AdminAuthorizationPolicy { get; set; }

    /// <summary>Maximum attachment bytes in one report manifest (default 100 MiB).</summary>
    public long MaximumReportAttachmentBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>Maximum bytes per attachment (default 10 MiB).</summary>
    public long MaximumAttachmentBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Maximum attachment count per report (default 1000).</summary>
    public int MaximumAttachmentsPerReport { get; set; } = 1000;

    /// <summary>Positive upload request body limit before decompression (default 10 MiB). Also applies to uncompressed requests.</summary>
    public long MaxCompressedUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Positive byte limit for decompressed JSON and its UTF-8 representation (default 10 MiB). Also bounds legacy reference expansion.</summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Positive full-report cache budget, measured in serialized UTF-8 bytes (default 64 MiB).
    /// This is a size estimate, not a limit on process memory. Configure before serving requests.
    /// </summary>
    public long ReportCacheSizeLimitBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Positive full-report retention after upload or an ordinary read (default eight hours).
    /// Bulk reads do not extend retention. Capacity eviction may remove reports sooner.
    /// </summary>
    public TimeSpan ReportCacheRetention { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Enables security headers on /bdtest responses. Disable only when the host supplies its own
    /// compatible policy. Register UseBDTestReportServer before endpoint/authentication middleware.
    /// </summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    /// <summary>The secret required in the X-BDTest-Upload-Key header. Uploads are disabled when unset.</summary>
    public string? UploadApiKey { get; set; }

    /// <summary>Maximum authenticated upload attempts per minute, shared by all clients of this server instance.</summary>
    public int UploadsPerMinute { get; set; } = 60;
}
