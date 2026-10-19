using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportUrlTests
{
    [Test]
    [Arguments("")]
    [Arguments("/reports")]
    public async Task Pagination_stays_on_the_browser_origin_and_keeps_filters(string pathBase)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("attacker.example:8080");
        context.Request.PathBase = pathBase;
        context.Request.Path = "/bdtest/report/run/all-scenarios";
        context.Request.QueryString = new QueryString("?Tag=release%26candidate&Page=1&Page=3");

        await Assert.That(context.GetUrlForPageNumber(2)).IsEqualTo(pathBase + "/bdtest/report/run/all-scenarios?Tag=release%26candidate&Page=2");
    }

    [Test]
    [Arguments("//attacker.example/path")]
    [Arguments("/\\attacker.example/path")]
    public async Task Local_paths_cannot_be_network_path_references(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var url = context.GetLocalUrl();
        await Assert.That(new Uri(new Uri("https://browser.example"), url).Host).IsEqualTo("browser.example");
    }

    [Test]
    [Arguments("")]
    [Arguments("/team's reports")]
    public async Task Rendered_pagination_keeps_the_path_base_and_query_without_a_host(string pathBase)
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_PaginationButtons.cshtml",
            new PaginationInformation { CurrentPage = 2, StartPage = 1, EndPage = 3, TotalPages = 3 },
            query: "?filter=Passed&page=2", pathBase: pathBase, host: "attacker.example:8080");
        var links = Regex.Matches(html, @"<a\b[^>]*href=""([^""]+)""").Select(m => HttpUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
        await Assert.That(links).IsNotEmpty();
        var prefix = new PathString(pathBase).ToUriComponent();

        await Assert.That((links).All(item => item.StartsWith(prefix + "/bdtest/"))).IsTrue();

        await Assert.That((links).All(item => item.Contains("filter=Passed"))).IsTrue();

        await Assert.That((links).Any(item => item.EndsWith("page=all"))).IsTrue();
        await Assert.That(html).DoesNotContain("attacker.example");
    }

    [Test]
    public async Task Summary_status_tiles_use_local_urls_with_an_escaped_report_id()
    {
        var report = new BDTestOutputModel
        {
            Id = "O'Brien",
            TestTimer = new TestTimer(),
            Scenarios = ReportJson.DeserializeObject<List<Scenario>>("""
                [
                  { "Guid": "one", "Status": "Passed", "Steps": [], "CustomTestInformation": [] },
                  { "Guid": "two", "Status": "Failed", "Steps": [], "CustomTestInformation": [] },
                  { "Guid": "three", "Status": "Pending", "Steps": [], "CustomTestInformation": [] }
                ]
                """)
        };
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Summary.cshtml", ReportPageModels.Create((report).ToReport()).Summary,
            pathBase: "/reports", host: "attacker.example", requestPath: "/bdtest/report/O'Brien/summary",
            configureViewData: viewData => viewData[nameof(ReportLayoutViewModel)] = ReportLayoutViewModel.Create((report).ToReport()));
        var urls = Regex.Matches(html, "<a class=\"tile\" data-status=\"(?!all)[^\"]+\" href=\"([^\"]+)\"")
            .Select(m => HttpUtility.HtmlDecode(m.Groups[1].Value)).ToArray();

        await Assert.That(urls).IsEquivalentTo(new[]
        {
            "/reports/bdtest/report/O%27Brien/all-scenarios?filterByStatus=Failed",
            "/reports/bdtest/report/O%27Brien/all-scenarios?filterByStatus=Pending",
            "/reports/bdtest/report/O%27Brien/all-scenarios?filterByStatus=Passed"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(html).DoesNotContain("onclick=");
        await Assert.That(html).DoesNotContain("attacker.example");
    }
}
