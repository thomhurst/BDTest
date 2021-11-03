using System;
using System.Collections.Generic;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.AspNetCore.Http;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IUserPersonalizer
    {
        Uri? GetProfilePictureUri(HttpContext httpContext);
        string? GetNameOfUser(HttpContext httpContext);
        List<CustomLinkData> GetLinksForUser(HttpContext httpContext);
    }
}