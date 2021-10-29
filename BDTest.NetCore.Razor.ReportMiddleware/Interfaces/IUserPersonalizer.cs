using System;
using Microsoft.AspNetCore.Http;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IUserPersonalizer
    {
        Uri? GetProfilePictureUri(HttpContext httpContext);
        string? GetNameOfUser(HttpContext httpContext);
    }
}