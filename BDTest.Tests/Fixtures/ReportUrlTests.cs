using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Views.BDTest;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportUrlTests
{
    [TestCase("")]
    [TestCase("/reports")]
    public void Pagination_stays_on_the_browser_origin_and_keeps_filters(string pathBase)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("attacker.example:8080");
        context.Request.PathBase = pathBase;
        context.Request.Path = "/bdtest/report/run/all-scenarios";
        context.Request.QueryString = new QueryString("?Tag=release%26candidate&Page=1&Page=3");

        Assert.That(context.GetUrlForPageNumber(2),
            Is.EqualTo(pathBase + "/bdtest/report/run/all-scenarios?Tag=release%26candidate&Page=2"));
    }

    [TestCase("//attacker.example/path")]
    [TestCase("/\\attacker.example/path")]
    public void Local_paths_cannot_be_network_path_references(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var url = context.GetLocalUrl();
        Assert.That(new Uri(new Uri("https://browser.example"), url).Host, Is.EqualTo("browser.example"));
    }

    [TestCase("")]
    [TestCase("/team's reports")]
    public async Task Rendered_pagination_keeps_the_path_base_and_query_without_a_host(string pathBase)
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_PaginationButtons.cshtml",
            new PaginationInformation { CurrentPage = 2, StartPage = 1, EndPage = 3, TotalPages = 3 },
            query: "?filter=Passed&page=2", pathBase: pathBase, host: "attacker.example:8080");
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(m => HttpUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
        Assert.That(links, Is.Not.Empty);
        var prefix = new PathString(pathBase).ToUriComponent();
        Assert.That(links, Has.All.StartsWith(prefix + "/bdtest/"));
        Assert.That(links, Has.All.Contains("filter=Passed"));
        Assert.That(links, Has.Some.EndsWith("page=all"));
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }

    [Test]
    public async Task Redirect_serializes_a_local_url_and_keeps_query_values_unchanged()
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_RedirectViaJavascript.cshtml",
            new RedirectViaJavascriptViewModel("/test-runs", "/summary"),
            query: "?filter=/test-runs&value=');alert(1);//", pathBase: "/reports", host: "attacker.example");
        var match = Regex.Match(html, @"window.location.href = (.*);");
        Assert.That(match.Success, Is.True);
        Assert.That(JsonSerializer.Deserialize<string>(match.Groups[1].Value),
            Is.EqualTo("/reports/bdtest/report/O'Brien/summary?filter=/test-runs&value=');alert(1);//"));
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }

    [TestCase("_Javascript_Flakey_Duration_Chart")]
    [TestCase("_Javascript_Flakey_Status_Chart")]
    public async Task Chart_navigation_uses_a_local_serialized_prefix(string view)
    {
        var html = await ReportHandlerEncodingTests.RenderAsync($"/Views/Shared/{view}.cshtml",
            new ChartViewModel { Scenarios = [], Reports = [] }, pathBase: "/team's reports", host: "attacker.example");
        var match = Regex.Match(html, @"window.location.href = (.*) \+ clickedOnReportId");
        Assert.That(match.Success, Is.True);
        Assert.That(JsonSerializer.Deserialize<string>(match.Groups[1].Value), Is.EqualTo("/team's reports/bdtest/report/"));
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }

    [Test]
    public async Task Summary_buttons_use_local_urls_and_preserve_filters()
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_SummaryGraphics.cshtml",
            new SummaryViewModel { Scenarios = [], TotalReportData = new BDTestOutputModel { TestTimer = new TestTimer() } },
            query: "?tag=O%27Brien", pathBase: "/reports", host: "attacker.example",
            requestPath: "/bdtest/report/run/summary");
        var handlers = Regex.Matches(html, "onclick=\"([^\"]+)\"")
            .Select(m => HttpUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
        Assert.That(handlers, Has.Length.EqualTo(9));
        var urls = handlers.Select(h => JsonSerializer.Deserialize<string>(h["window.location = ".Length..])).ToArray();
        Assert.That(urls, Has.All.StartsWith("/reports/bdtest/report/run/all-scenarios?"));
        Assert.That(urls, Has.All.Contains("tag=O%27Brien"));
        Assert.That(urls, Has.Some.Contains("filterByStatus=Passed"));
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }

    [Test]
    public async Task Pie_chart_uses_a_local_url_with_an_escaped_report_id()
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_TestTimesPieChart.cshtml",
            new TestTimesPieChartViewModel { Scenarios = [], ReportId = "O'Brien" },
            pathBase: "/reports", host: "attacker.example");
        var matches = Regex.Matches(html, @"window.location.href = (.*) \+ clickedOnScenarioId");
        Assert.That(matches, Has.Count.EqualTo(2));
        foreach (Match match in matches)
        {
            Assert.That(JsonSerializer.Deserialize<string>(match.Groups[1].Value),
                Is.EqualTo("/reports/bdtest/report/O%27Brien/scenario/"));
        }
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }
}
