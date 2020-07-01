using System.Collections.Generic;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.ReportGenerator.RazorServer
{
    public class CustomTabProvider : IBDTestCustomTabsProvider
    {
        public List<CustomTabData> GetCustomTabsData()
        {
            return new List<CustomTabData>
            {
                new CustomTabData("Google", "https://www.google.com")
            };
        }
    }
}