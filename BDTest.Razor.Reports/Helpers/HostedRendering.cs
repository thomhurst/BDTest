using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Storage;
using BDTest.Rendering;
using Microsoft.AspNetCore.Mvc;

namespace BDTest.Razor.Reports.Helpers;

internal static class HostedRendering
{
    internal static ReportRenderOptions Options(IUrlHelper url, ReportLayoutViewModel? layout, bool omitCustomHtml = false)
    {
        // Use the stored report identity, never a captured scenario ReportId or attachment URI.
        var reportId = layout?.Report?.Id;
        return new ReportRenderOptions
        {
            Hosted = true,
            OmitCustomHtml = omitCustomHtml,
            AttachmentUrl = file => AttachmentStorage.IsValidId(reportId) && AttachmentStorage.IsValidId(file.Id)
                ? url.Action("Download", "Attachments", new { reportId, attachmentId = file.Id }) : null
        };
    }
}
