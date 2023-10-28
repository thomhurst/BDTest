using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

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