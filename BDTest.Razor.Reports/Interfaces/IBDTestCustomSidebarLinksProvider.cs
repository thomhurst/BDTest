using System.Diagnostics.CodeAnalysis;
using BDTest.Contracts;
using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestCustomSidebarLinksProvider
{
    IEnumerable<CustomLinkData> GetCustomLinks([AllowNull] BDTestReport currentTestReport);
}