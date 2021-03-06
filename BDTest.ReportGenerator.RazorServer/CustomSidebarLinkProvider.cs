using System;
using System.Collections.Generic;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.ReportGenerator.RazorServer
{
    public class CustomSidebarLinkProvider : IBDTestCustomSidebarLinksProvider
    {
        public IEnumerable<CustomLinkData> GetCustomLinks(BDTestOutputModel currentTestReport)
        {
            return new List<CustomLinkData>
            {
                new CustomLinkData("Google", new Uri("https://www.google.com"))
            };
        }
    }
}