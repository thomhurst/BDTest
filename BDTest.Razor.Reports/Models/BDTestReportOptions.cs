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
    /// Enables security headers under the mapped report prefix. Disable only when the host supplies its own
    /// compatible policy. Register UseBDTestReportServer before endpoint/authentication middleware.
    /// </summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    private IReadOnlyList<string> _additionalImageSources = Array.Empty<string>();

    /// <summary>
    /// Extra HTTP(S) origins allowed by the report image policy, in addition to self, data: and https:.
    /// Assign before serving requests. Values must be origins without credentials, paths, queries or fragments.
    /// The assigned list is validated and copied; arbitrary CSP expressions are not supported.
    /// </summary>
    public IReadOnlyList<string> AdditionalImageSources
    {
        get => _additionalImageSources;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var origins = new string[value.Count];
            for (var i = 0; i < value.Count; i++)
            {
                var source = value[i];
                // Check the original path too: Uri normalizes dot segments such as /.. to /.
                if (string.IsNullOrWhiteSpace(source) || source.Any(char.IsWhiteSpace) || source.Contains(';') || source.Contains('*') ||
                    !Uri.TryCreate(source, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString() ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                    source.TrimEnd('/').AsSpan(uri.Scheme.Length + 3).Contains('/'))
                {
                    throw new ArgumentException("Image sources must be HTTP(S) origins without credentials, paths, queries or fragments.", nameof(value));
                }

                origins[i] = new UriBuilder(uri) { Host = uri.IdnHost }.Uri.GetLeftPart(UriPartial.Authority);
            }

            _additionalImageSources = Array.AsReadOnly(origins);
        }
    }


    /// <summary>The secret required in the X-BDTest-Upload-Key header. Uploads are disabled when unset.</summary>
    public string? UploadApiKey { get; set; }

    /// <summary>Maximum authenticated upload attempts per minute, shared by all clients of this server instance.</summary>
    public int UploadsPerMinute { get; set; } = 60;
}
