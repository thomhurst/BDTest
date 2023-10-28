using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

namespace BDTest.ReportGenerator.RazorServer;

public class UserPersonalizer : IUserPersonalizer
{
    public Uri GetProfilePictureUri(HttpContext httpContext)
    {
        return new Uri(
            "https://scontent-lcy1-1.xx.fbcdn.net/v/t1.6435-9/241727189_10226882023666952_6405596852781159718_n.jpg?_nc_cat=111&ccb=1-5&_nc_sid=09cbfe&_nc_ohc=t-KR1hoS7i4AX9sQUCz&_nc_oc=AQkqz7n2Nj9ryMhlOEDlbf1ciugw9daqevzE04Zj3fdym3qbD6_f3LJC50w5e7K4PNQ&_nc_ht=scontent-lcy1-1.xx&oh=d798ebe055361a34a2cfc5c196d3cc69&oe=61A150D8");
    }
        
    public string GetNameOfUser(HttpContext httpContext)
    {
        return "Tom Longhurst";
    }

    public List<CustomLinkData> GetLinksForUser(HttpContext httpContext)
    {
        return new List<CustomLinkData>
        {
            new("My Account", new Uri("https://myaccount.microsoft.com/"))
        };
    }
}