using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Interfaces;

/// <summary>
/// Authorizes privileged report operations, including report deletion.
/// </summary>
/// <remarks>
/// Implementations must require an authenticated user and verify an administrator role or policy
/// using trusted, validated identity claims. A client-supplied cookie, header, or query parameter
/// alone is not proof of authorization. Hosts must run authentication middleware before report endpoints.
/// </remarks>
public interface IAdminAuthorizer
{
    Task<bool> IsAdminAsync(HttpContext httpContext);
}
