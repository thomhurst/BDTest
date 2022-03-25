using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.AspNetCore.Http;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

public interface IUserPersonalizer
{
#nullable enable
    Uri? GetProfilePictureUri(HttpContext httpContext);
#nullable enable
    string? GetNameOfUser(HttpContext httpContext);
    List<CustomLinkData> GetLinksForUser(HttpContext httpContext);
}