using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportHeaderTests
{
    [TestCase(null, "span")]
    [TestCase("https://example.test/docs?one=1&two=2", "a")]
    [TestCase("/docs?one=1&two=2", "a")]
    public async Task Custom_header_entries_render_text_or_links(string url, string expectedElement)
    {
        const string text = "Header <text> & details";
        var link = new CustomLinkData(text, url == null ? null : new Uri(url, UriKind.RelativeOrAbsolute));
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_NavBarHeader.cshtml",
            new ReportLayoutViewModel(), customHeaderLinksProvider: new HeaderLinksProvider(link));
        var matches = Regex.Matches(html, """<(span|a) class="dropdown-item"[^>]*>.*?</\1>""", RegexOptions.Singleline);
        var element = matches.Select(match => XElement.Parse(match.Value)).Single(x => x.Value == text);

        Assert.That(element.Name.LocalName, Is.EqualTo(expectedElement));
        Assert.That(element.Value, Is.EqualTo(text));
        if (expectedElement == "span")
        {
            Assert.That(element.Attributes().Select(x => x.Name.LocalName), Is.EqualTo(new[] { "class" }));
        }
        else
        {
            Assert.That((string)element.Attribute("href"), Is.EqualTo(url));
            Assert.That((string)element.Attribute("target"), Is.EqualTo("_blank"));
        }
    }

    private sealed class HeaderLinksProvider(CustomLinkData link) : IBDTestCustomHeaderLinksProvider
    {
        public IEnumerable<CustomLinkData> GetCustomLinks() => [link];
    }
}
