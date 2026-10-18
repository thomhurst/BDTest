using System.Text;
using System.Text.Json;
using BDTest.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;

namespace BDTest.Razor.Reports.Implementations;

// Only the BDTest upload endpoint uses this reader. Host formatters and JSON options stay independent.
internal sealed class ReportModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var request = bindingContext.HttpContext.Request;
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

        using var utf8Body = string.Equals(charset, "utf-16", StringComparison.OrdinalIgnoreCase)
            ? Encoding.CreateTranscodingStream(request.Body, Encoding.Unicode, Encoding.UTF8, leaveOpen: true)
            : null;
        try
        {
            var report = await BDTestJsonHelper.DeserializeAsync(utf8Body ?? request.Body, bindingContext.HttpContext.RequestAborted,
                maximumExpandedSize: 10 * 1024 * 1024);
            bindingContext.Result = ModelBindingResult.Success(report);
        }
        catch (JsonException)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid report JSON.");
        }
    }
}
