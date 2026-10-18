using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BDTest.Razor.Reports.Implementations;

// Applied to both controllers, so new report actions inherit the same secure default.
public sealed class ReportAuthorizationFilter(
    bool requireAdmin,
    IOptions<BDTestReportOptions> configuredOptions,
    IAuthorizationPolicyProvider policyProvider,
    IPolicyEvaluator policyEvaluator,
    ILogger<ReportAuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    private readonly BDTestReportOptions options = configuredOptions.Value;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Uploads use their own API key. Ping and error handling are also explicitly public.
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;

        var policyName = requireAdmin ? options.AdminAuthorizationPolicy : options.ReportsAuthorizationPolicy;
        if (string.IsNullOrWhiteSpace(policyName))
        {
            // Explicit endpoint authorization is enforced by the host's authorization middleware.
            // A fallback policy alone is not an explicit report-access configuration.
            var endpoint = context.HttpContext.GetEndpoint();
            if (!requireAdmin && (options.AllowAnonymousReports
                || endpoint?.Metadata.GetMetadata<IAuthorizeData>() != null
                || endpoint?.Metadata.GetMetadata<AuthorizationPolicy>() != null)) return;
            logger.LogWarning("BDTest {Access} access denied: configure {Option}{AnonymousOption}.",
                requireAdmin ? "delete" : "read",
                requireAdmin ? nameof(options.AdminAuthorizationPolicy) : nameof(options.ReportsAuthorizationPolicy),
                requireAdmin ? "" : " or explicitly set AllowAnonymousReports = true");
            context.Result = new StatusCodeResult(403);
            return;
        }

        var policy = await policyProvider.GetPolicyAsync(policyName);
        if (policy == null)
        {
            logger.LogError("BDTest access denied: authorization policy {Policy} is not registered.", policyName);
            context.Result = new StatusCodeResult(403);
            return;
        }

        var reader = context.HttpContext.User;
        try
        {
            var authentication = await policyEvaluator.AuthenticateAsync(policy, context.HttpContext);
            var authorization = await policyEvaluator.AuthorizeAsync(policy, authentication, context.HttpContext, context.HttpContext);
            if (authorization.Succeeded) return;

            var schemes = policy.AuthenticationSchemes.ToArray();
            context.Result = authorization.Challenged ? new ChallengeResult(schemes) : new ForbidResult(schemes);
        }
        finally
        {
            // Keep antiforgery validation bound to the same reader that rendered the form.
            if (requireAdmin) context.HttpContext.User = reader;
        }
    }
}
