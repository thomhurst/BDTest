using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class PagerExtensionsTests
{
    [TestCase(25, 0, 1)]
    [TestCase(25, 24, 1)]
    [TestCase(25, 25, 2)]
    [TestCase(25, 49, 2)]
    [TestCase(25, 50, 3)]
    [TestCase(25, 51, 3)]
    [TestCase(10, 9, 1)]
    [TestCase(10, 10, 2)]
    [TestCase(1, 1, 2)]
    public void ItemLookupReturnsThePageContainingTheItem(int pageSize, int itemIndex, int expectedPage)
    {
        var items = Enumerable.Range(0, 52).Select(index => $"Item {index}").ToArray();
        var pager = new Pager<string>(items, "1", pageSize);
        var item = items[itemIndex];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pager.GetPageNumberOfItem(item), Is.EqualTo(expectedPage));
            Assert.That(pager.GetPageNumberWhere(candidate => candidate == item), Is.EqualTo(expectedPage));
        }

        var destinationPage = new Pager<string>(items, expectedPage.ToString(), pageSize);
        Assert.That(destinationPage.ItemsForCurrentPage, Does.Contain(item));
    }
}
