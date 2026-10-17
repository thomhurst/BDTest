using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

namespace BDTest.ReportGenerator.RazorServer;

public class UserPersonalizer : IUserPersonalizer
{
    public Uri GetProfilePictureUri(HttpContext httpContext)
    {
        return null;
    }

    public string GetNameOfUser(HttpContext httpContext)
    {
        var identity = httpContext.User.Identity;
        return identity?.IsAuthenticated == true ? identity.Name : null;
    }

    public List<CustomLinkData> GetLinksForUser(HttpContext httpContext)
    {
        return new List<CustomLinkData>
        {
            new("My Account", new Uri("https://myaccount.microsoft.com/"))
        };
    }
}