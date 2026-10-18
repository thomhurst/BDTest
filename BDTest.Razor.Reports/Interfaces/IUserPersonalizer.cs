using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Interfaces;

public interface IUserPersonalizer
{
    Uri? GetProfilePictureUri(HttpContext httpContext);
    string? GetNameOfUser(HttpContext httpContext);
    List<CustomLinkData> GetLinksForUser(HttpContext httpContext);
}