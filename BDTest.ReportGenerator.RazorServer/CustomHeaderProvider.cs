using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.ReportGenerator.RazorServer;

public class CustomHeaderProvider : IBDTestCustomHeaderLinksProvider
{
    public IEnumerable<CustomLinkData> GetCustomLinks()
    {
        return new List<CustomLinkData>
        {
            new("Hi, Tom!", null)
        };
    }
}