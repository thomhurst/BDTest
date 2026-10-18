using System.Net.Http.Headers;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;

namespace BDTest.Razor.Reports.Implementations;

internal static class AttachmentManifest
{
    internal static bool IsValid(BDTestOutputModel report, BDTestReportServerOptions options, bool enforceUploadLimits = true)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var file in BDTestAttachmentHelper.GetAttachments(report))
        {
            if (options.AttachmentStore == null || file == null || !AttachmentStorage.IsValidId(file.Id)
                || !ids.Add(file.Id) || file.Length < 0 || file.AttemptNumber < 1
                || enforceUploadLimits && (ids.Count > options.MaximumAttachmentsPerReport
                    || file.Length > options.MaximumAttachmentBytes
                    || file.Length > options.MaximumReportAttachmentBytes - total)
                || string.IsNullOrWhiteSpace(file.Name) || file.Name.Length > 255
                || file.Name.Any(character => char.IsControl(character) || character is '/' or '\\')
                || file.MimeType == null || file.MimeType.Length > 256 || file.MimeType.Any(char.IsControl)
                || !MediaTypeHeaderValue.TryParse(file.MimeType, out var mime) || mime.MediaType.Contains('*')
                || file.Uri != null)
                return false;
            if (enforceUploadLimits) total += file.Length;
        }
        return true;
    }
}
