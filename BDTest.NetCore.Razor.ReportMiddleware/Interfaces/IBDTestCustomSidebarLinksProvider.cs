using System.Diagnostics.CodeAnalysis;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Models;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

public interface IBDTestCustomSidebarLinksProvider
{
    IEnumerable<CustomLinkData> GetCustomLinks([AllowNull] BDTestOutputModel currentTestReport);
}