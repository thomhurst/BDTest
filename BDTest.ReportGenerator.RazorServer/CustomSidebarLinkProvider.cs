using BDTest.Contracts;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

namespace BDTest.ReportGenerator.RazorServer;

public class CustomSidebarLinkProvider : IBDTestCustomSidebarLinksProvider
{
    public IEnumerable<CustomLinkData> GetCustomLinks(BDTestReport? currentTestReport)
    {
        return new List<CustomLinkData>
        {
            new("Google", new Uri("https://www.google.com"))
        };
    }
}