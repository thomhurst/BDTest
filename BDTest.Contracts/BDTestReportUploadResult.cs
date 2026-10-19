using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>The report address and server-assigned identity returned after an upload.</summary>
public sealed class BDTestReportUploadResult
{
    /// <summary>Creates an upload result. Legacy servers may omit the report ID.</summary>
    [JsonConstructor]
    public BDTestReportUploadResult(Uri reportUrl, string? reportId)
    {
        ReportUrl = reportUrl ?? throw new ArgumentNullException(nameof(reportUrl));
        ReportId = reportId;
    }

    [JsonPropertyName("reportUrl")]
    public Uri ReportUrl { get; }

    /// <summary>Server-assigned ID, or null when a legacy server supplied no receipt.</summary>
    [JsonPropertyName("reportId")]
    public string? ReportId { get; }
}
