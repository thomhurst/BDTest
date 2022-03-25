using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

namespace BDTest.ReportGenerator.RazorServer;

public class AdminAuthorizer : IAdminAuthorizer
{
    public Task<bool> IsAdminAsync(HttpContext httpContext)
    {
        return Task.FromResult(httpContext.Request.Cookies.ContainsKey("admin"));
    }
}