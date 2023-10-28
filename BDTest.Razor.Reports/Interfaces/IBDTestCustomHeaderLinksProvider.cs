using BDTest.Razor.Reports.Models;

namespace BDTest.Razor.Reports.Interfaces;

public interface IBDTestCustomHeaderLinksProvider
{
    IEnumerable<CustomLinkData> GetCustomLinks();
}