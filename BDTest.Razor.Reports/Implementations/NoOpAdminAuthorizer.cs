using BDTest.Razor.Reports.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Implementations;

public class NoOpAdminAuthorizer : IAdminAuthorizer
{
    public Task<bool> IsAdminAsync(HttpContext httpContext)
    {
        return Task.FromResult(false);
    }
}