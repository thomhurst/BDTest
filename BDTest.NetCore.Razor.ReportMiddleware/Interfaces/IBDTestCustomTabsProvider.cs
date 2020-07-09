using System.Collections.Generic;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IBDTestCustomTabsProvider
    {
        IEnumerable<CustomLinkData> GetCustomLinks();
    }
}