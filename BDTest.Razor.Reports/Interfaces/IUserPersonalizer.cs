using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Interfaces;

public interface IUserPersonalizer
{
#nullable enable
    Uri? GetProfilePictureUri(HttpContext httpContext);
#nullable enable
    string? GetNameOfUser(HttpContext httpContext);
    List<CustomLinkData> GetLinksForUser(HttpContext httpContext);
}