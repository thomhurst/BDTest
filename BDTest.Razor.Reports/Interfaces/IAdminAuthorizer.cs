using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Interfaces;

public interface IAdminAuthorizer
{
    Task<bool> IsAdminAsync(HttpContext httpContext);
}