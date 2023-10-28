using System.Diagnostics.CodeAnalysis;
using BDTest.Maps;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestCustomSidebarLinksProvider
{
    IEnumerable<CustomLinkData> GetCustomLinks([AllowNull] BDTestOutputModel currentTestReport);
}