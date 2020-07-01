using System.Collections.Generic;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.ReportGenerator.RazorServer
{
    public class CustomTabProvider : IBDTestCustomTabsProvider
    {
        public List<CustomLinkData> GetCustomLinks()
        {
            return new List<CustomLinkData>
            {
                new CustomLinkData("Google", "https://www.google.com")
            };
        }
    }
}