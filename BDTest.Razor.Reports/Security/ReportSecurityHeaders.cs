using System.Security.Cryptography;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Security;

internal static class ReportSecurityHeaders
{
    private static readonly object NonceKey = new();

    internal static string GetNonce(HttpContext context)
    {
        if (context.Items.TryGetValue(NonceKey, out var existing) && existing is string nonceValue)
        {
            return nonceValue;
        }

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        context.Items[NonceKey] = nonce;
        return nonce;
    }

    internal static void Apply(HttpContext context, BDTestReportOptions options, bool preventCaching = true)
    {
        var nonce = GetNonce(context);
        var imageSources = "'self' data: https:";
        if (options.AdditionalImageSources.Count > 0)
        {
            imageSources += " " + string.Join(" ", options.AdditionalImageSources);
        }

        // Append so any policy supplied by the host remains enforced as well.
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'none'; " +
            $"script-src 'nonce-{nonce}'; script-src-attr 'none'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            $"img-src {imageSources}; " +
            "font-src 'self' data: https://fonts.gstatic.com; connect-src 'self'; frame-src 'self'; " +
            "object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        // Cached HTML must not reuse a nonce across responses.
        if (preventCaching) context.Response.Headers["Cache-Control"] = "no-store";
    }
}
