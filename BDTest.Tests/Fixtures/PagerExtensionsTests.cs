using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class PagerExtensionsTests
{
    [Test]
    [Arguments(25, 0, 1)]
    [Arguments(25, 24, 1)]
    [Arguments(25, 25, 2)]
    [Arguments(25, 49, 2)]
    [Arguments(25, 50, 3)]
    [Arguments(25, 51, 3)]
    [Arguments(10, 9, 1)]
    [Arguments(10, 10, 2)]
    [Arguments(1, 1, 2)]
    public async Task ItemLookupReturnsThePageContainingTheItem(int pageSize, int itemIndex, int expectedPage)
    {
        var items = Enumerable.Range(0, 52).Select(index => $"Item {index}").ToArray();
        var pager = new Pager<string>(items, "1", pageSize);
        var item = items[itemIndex];

        using (Assert.Multiple())
        {
            await Assert.That(pager.GetPageNumberOfItem(item)).IsEqualTo(expectedPage);
            await Assert.That(pager.GetPageNumberWhere(candidate => candidate == item)).IsEqualTo(expectedPage);
        }

        var destinationPage = new Pager<string>(items, expectedPage.ToString(), pageSize);
        await Assert.That(destinationPage.ItemsForCurrentPage).Contains(item);
    }
}
