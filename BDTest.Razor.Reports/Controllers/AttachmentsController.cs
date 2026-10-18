using BDTest.Helpers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BDTest.Razor.Reports.Controllers;

[Route("")]
[TypeFilter(typeof(ReportAuthorizationFilter), Arguments = new object[] { false })]
public sealed class AttachmentsController(IDataRepository repository, IOptions<BDTestReportOptions> configuredOptions,
    ReportAttachmentGate attachmentGate, IBDTestAttachmentStore attachmentStore = null) : Controller
{
    private readonly BDTestReportOptions options = configuredOptions.Value;

    [HttpPost("data/{reportId}/attachments/{attachmentId}")]
    [ServiceFilter(typeof(ReportUploadFilter))]
    public async Task<IActionResult> Upload(string reportId, string attachmentId, CancellationToken cancellationToken)
    {
        if (!AttachmentStorage.IsValidId(reportId) || !AttachmentStorage.IsValidId(attachmentId)) return BadRequest();
        var tokens = Request.Headers["X-BDTest-Attachment-Token"];
        if (tokens.Count != 1 || tokens[0]?.Length != 64) return Unauthorized();
        if (attachmentStore == null) return NotFound();

        var report = await repository.GetDataForBulkRead(reportId, cancellationToken);
        if (report == null || !AttachmentManifest.IsValid(report, options, attachmentStore != null)) return NotFound();
        if (!AttachmentUploadAuthorization.IsValid(report.AttachmentUploadTokenHash, tokens[0])) return Unauthorized();
        var file = BDTestAttachmentHelper.GetAttachments(report).SingleOrDefault(file => file.Id == attachmentId);
        if (file == null) return NotFound();
        if (Request.ContentLength.HasValue && Request.ContentLength != file.Length) return BadRequest();
        var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = file.Length;

        // Network reads never hold a gate needed by another upload or report cleanup.
        await using var staging = new FileStream(Path.Combine(Path.GetTempPath(), "bdtest-upload-" + Guid.NewGuid().ToString("N")),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        try
        {
            await AttachmentStorage.CopyExactlyAsync(Request.Body, staging, file.Length, cancellationToken);
        }
        catch (InvalidDataException) { return BadRequest(); }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413) { return StatusCode(413); }
        staging.Position = 0;

        // Revalidate after staging so deletion or expiry cannot resurrect attachment content.
        using var lease = await attachmentGate.EnterAsync(reportId, cancellationToken);
        report = await repository.GetDataForBulkRead(reportId, cancellationToken);
        if (report == null || !AttachmentManifest.IsValid(report, options, attachmentStore != null)) return NotFound();
        if (!AttachmentUploadAuthorization.IsValid(report.AttachmentUploadTokenHash, tokens[0])) return Unauthorized();
        var currentFile = BDTestAttachmentHelper.GetAttachments(report).SingleOrDefault(current => current.Id == attachmentId);
        if (currentFile == null || currentFile.Length != file.Length) return NotFound();
        await attachmentStore.WriteAsync(reportId, attachmentId, staging, file.Length, cancellationToken);
        return NoContent();
    }

    [HttpGet("report/{reportId}/attachments/{attachmentId}")]
    public async Task<IActionResult> Download(string reportId, string attachmentId, CancellationToken cancellationToken)
    {
        if (!AttachmentStorage.IsValidId(reportId) || !AttachmentStorage.IsValidId(attachmentId) || attachmentStore == null)
            return NotFound();
        var report = await repository.GetDataForBulkRead(reportId, cancellationToken);
        if (report == null || !AttachmentManifest.IsValid(report, options, attachmentStore != null, enforceUploadLimits: false)) return NotFound();
        var file = BDTestAttachmentHelper.GetAttachments(report).SingleOrDefault(file => file.Id == attachmentId);
        if (file == null) return NotFound();
        var stream = await attachmentStore.OpenReadAsync(reportId, attachmentId, cancellationToken);
        if (stream == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        // Always download, including HTML and SVG. MVC escapes the display name and owns the stream.
        return File(stream, file.MimeType, file.Name);
    }
}
