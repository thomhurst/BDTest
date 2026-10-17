using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BDTest.Razor.Reports.Implementations;

// Registered as a singleton so that every upload shares the same bounded limiter.
internal sealed class ReportUploadFilter : IAuthorizationFilter, IDisposable
{
    private readonly BDTestReportServerOptions _options;
    private readonly FixedWindowRateLimiter _limiter;

    public ReportUploadFilter(BDTestReportServerOptions options)
    {
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
        }
    }

    public void Dispose() => _limiter.Dispose();
}
