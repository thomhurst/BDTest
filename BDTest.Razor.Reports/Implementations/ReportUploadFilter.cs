using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace BDTest.Razor.Reports.Implementations;

// Registered as a singleton so that every upload shares the same bounded limiter.
internal sealed class ReportUploadFilter : IAuthorizationFilter, IExceptionFilter, IDisposable
{
    private readonly BDTestReportOptions _options;
    private readonly FixedWindowRateLimiter _limiter;

    public ReportUploadFilter(IOptions<BDTestReportOptions> configuredOptions)
    {
        var options = configuredOptions.Value;
        _options = options;
        _limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.UploadsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var suppliedKeys = request.Headers["X-BDTest-Upload-Key"];
        if (string.IsNullOrWhiteSpace(_options.UploadApiKey) || suppliedKeys.Count != 1 ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(_options.UploadApiKey)),
                SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKeys[0] ?? string.Empty))))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        using var lease = _limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                ? Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds))
                : 60;
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            return;
        }

        // The bounded reader enforces body bytes, excluding HTTP chunk framing. Kestrel's
        // chunked limit also counts framing, so leave that limit to the endpoint reader.
        var bodySize = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = null;
        }

        if (request.ContentLength > _options.MaxCompressedUploadBytes)
        {
            context.Result = new ObjectResult("Report request exceeds MaxCompressedUploadBytes.")
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge
            };
        }
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } exception)
        {
            context.Result = new ObjectResult(exception.Message) { StatusCode = exception.StatusCode };
            context.ExceptionHandled = true;
        }
    }

    public void Dispose() => _limiter.Dispose();
}
