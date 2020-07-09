using System.Collections.Generic;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces
{
    public interface IBDTestCustomHeaderLinksProvider
    {
        IEnumerable<CustomLinkData> GetCustomLinks();
    }
}