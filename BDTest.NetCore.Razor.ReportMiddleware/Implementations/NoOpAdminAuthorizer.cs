using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BDTest.NetCore.Razor.ReportMiddleware.Implementations;

public class NoOpAdminAuthorizer : IAdminAuthorizer
{
    public Task<bool> IsAdminAsync(HttpContext httpContext)
    {
        return Task.FromResult(false);
    }
}