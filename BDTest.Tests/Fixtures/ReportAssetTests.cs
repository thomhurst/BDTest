using System.Text.RegularExpressions;
using BDTest.Razor.Reports.Models.ViewModels;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportAssetTests
{
    [TestCase(true, "toggle_off-24px.svg")]
    [TestCase(false, "toggle_on-24px.svg")]
    public async Task Collapsable_article_uses_report_library_icons(bool startCollapsed, string icon)
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_CollapsableArticle.cshtml",
            new CollapsableArticicleViewModel
            {
                Header = "Output", Body = "Test output", StartCollapsed = startCollapsed
            }, pathBase: "/reports");

        var source = Regex.Match(html, "src=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(source, Is.EqualTo("/reports/_content/BDTest.Razor.Reports/icons/" + icon));
    }
}
