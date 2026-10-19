using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace BDTest.Razor.Reports.Implementations;

// Only the BDTest upload endpoint uses this reader. Host formatters and JSON options stay independent.
internal sealed class ReportModelBinder(IOptions<BDTestReportOptions> configuredOptions) : IModelBinder
{
    private readonly BDTestReportOptions options = configuredOptions.Value;
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var request = bindingContext.HttpContext.Request;
        var contentEncoding = request.Headers.ContentEncoding;
        var gzip = contentEncoding.Count == 1 && string.Equals(contentEncoding[0]?.Trim(), "gzip", StringComparison.OrdinalIgnoreCase);
        if (contentEncoding.Count != 0 && !gzip)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName,
                new UnsupportedContentTypeException("Expected no Content-Encoding or a single gzip encoding."), bindingContext.ModelMetadata);
            return;
        }
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !(contentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                 || contentType.MediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
                 || (contentType.MediaType.Value?.StartsWith("application/", StringComparison.OrdinalIgnoreCase) == true
                     && contentType.MediaType.Value.EndsWith("+json", StringComparison.OrdinalIgnoreCase))))
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName,
                new UnsupportedContentTypeException("Expected a JSON report."), bindingContext.ModelMetadata);
            return;
        }

        var charset = HeaderUtilities.RemoveQuotes(contentType.Charset).Value;
        if (!string.IsNullOrEmpty(charset)
            && !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
            && !charset.Equals("utf-16", StringComparison.OrdinalIgnoreCase))
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName,
                new UnsupportedContentTypeException("Expected UTF-8 or UTF-16 report JSON."), bindingContext.ModelMetadata);
            return;
        }

        using var wireBody = new UploadLimitStream(request.Body, options.MaxCompressedUploadBytes, nameof(options.MaxCompressedUploadBytes));
        using var gzipBody = gzip ? new GZipStream(wireBody, CompressionMode.Decompress, leaveOpen: true) : null;
        using var expandedBody = new UploadLimitStream(gzipBody ?? (Stream)wireBody, options.MaxUploadBytes, nameof(options.MaxUploadBytes));
        using var utf8Body = string.Equals(charset, "utf-16", StringComparison.OrdinalIgnoreCase)
            ? Encoding.CreateTranscodingStream(expandedBody, Encoding.Unicode, Encoding.UTF8, leaveOpen: true)
            : null;
        using var transcodedBody = utf8Body is null
            ? null
            : new UploadLimitStream(utf8Body, options.MaxUploadBytes, nameof(options.MaxUploadBytes));
        try
        {
            var cancellationToken = bindingContext.HttpContext.RequestAborted;
            var report = await HostedReportJson.DeserializeAsync(transcodedBody ?? expandedBody, cancellationToken,
                maximumExpandedSize: options.MaxUploadBytes);
            // A gzip reader can finish before consuming trailing wire bytes. Enforce the entire request budget.
            await wireBody.CopyToAsync(Stream.Null, cancellationToken);
            bindingContext.Result = ModelBindingResult.Success(report);
        }
        catch (JsonException)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid report JSON.");
        }
        catch (InvalidDataException)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid gzip report body.");
        }
    }
}
