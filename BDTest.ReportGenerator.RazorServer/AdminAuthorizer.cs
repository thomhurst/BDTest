using BDTest.Razor.Reports.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace BDTest.ReportGenerator.RazorServer;

public class AdminAuthorizer : IAdminAuthorizer
{
    public const string PolicyName = "BDTest.Administrator";
    public const string RoleName = "BDTest.Admin";

    public async Task<bool> IsAdminAsync(HttpContext httpContext)
    {
        // This authorizer is shared by report options; resolve authorization in the request scope.
        var authorization = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(httpContext.User, httpContext, PolicyName);
        return result.Succeeded;
    }
}
