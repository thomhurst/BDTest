using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class HistoryTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 2, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Values =
    [
        "release-1.2",
        "O'Brien's build",
        "');alert(1);//",
        "\" onmouseover=\"alert(1)",
        "</script><script>alert(1)</script>",
        "quotes '\" & < > \\ and Unicode café 雪",
        "line one\nline two"
    ];

    [Test]
    public async Task Filter_options_are_encoded_data([ValueSource(nameof(Values))] string value)
    {
        var run = Run("run", value, Start, failed: 0);
        run.Environment = value;
        run.BranchName = value;
        run.MachineName = value;
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_HistoryHeader.cshtml",
            new HistoryViewModel { AllRuns = [run], Runs = [run], Filter = new RunFilter { Tag = value } });

        var options = Regex.Matches(html, "<option value=\"[^\"]*\"[^>]*>[^<]*</option>")
            .Select(match => XElement.Parse(match.Value.Replace(" selected>", " selected=\"selected\">")))
            .Where(option => Required(option.Attribute("value")).Value != "" && !RunFilter.AllRanges.Contains(Required(option.Attribute("value")).Value))
            .ToList();
        Assert.That(options, Has.Count.EqualTo(4), "Tag, environment, branch and machine each offer the value once.");
        Assert.That(options.Select(option => Required(option.Attribute("value")).Value), Has.All.EqualTo(value));
        Assert.That(options.Select(option => option.Value), Has.All.EqualTo(value));
        Assert.That(options.Count(option => option.Attribute("selected") != null), Is.EqualTo(1));
        Assert.That(html, Does.Not.Contain("<script>alert(1)"));
        Assert.That(html, Does.Not.Contain("onmouseover=\"alert"));
    }

    [Test]
    public async Task Comparison_tabs_keep_the_number_of_compared_runs()
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_HistoryHeader.cshtml",
            new HistoryViewModel { Tab = HistoryViewModel.Flaky, Compare = 50, Filter = new RunFilter { Tag = "nightly" } });

        var tabs = Regex.Matches(html, "<a class=\"tab[^\"]*\" href=\"([^\"]+)\"")
            .Select(match => System.Web.HttpUtility.HtmlDecode(match.Groups[1].Value)).ToList();
        Assert.That(tabs.Single(href => href.Contains("/history/timing")), Does.Contain("compare=50").And.Contain("tag=nightly"));
        Assert.That(tabs.Single(href => href.Contains("/history/flaky")), Does.Contain("compare=50"));
        Assert.That(tabs.Single(href => !href.Contains("/history/")), Does.Not.Contain("compare="));
    }

    [Test]
    public async Task Trend_charts_link_each_run_locally_and_encode_dates()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = new CultureInfo("en-US");
            culture.DateTimeFormat.AbbreviatedMonthNames = ["\"雪\" <tag>", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", ""];
            culture.DateTimeFormat.AbbreviatedMonthGenitiveNames = culture.DateTimeFormat.AbbreviatedMonthNames;
            CultureInfo.CurrentCulture = culture;
            var runs = new List<TestRunSummary>
            {
                Run("O'Brien", "nightly", new DateTime(2026, 1, 5, 2, 0, 0, DateTimeKind.Utc), failed: 1),
                Run("second", "nightly", new DateTime(2026, 1, 6, 2, 0, 0, DateTimeKind.Utc), failed: 0)
            };

            var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Trends.cshtml",
                new HistoryViewModel { AllRuns = runs, Runs = runs }, pathBase: "/team's reports", host: "attacker.example",
                requestPath: "/bdtest/report/history");

            Assert.That(html, Does.Contain("&quot;&#x96EA;&quot; &lt;tag&gt;").Or.Contain("&quot;雪&quot; &lt;tag&gt;"));
            Assert.That(html, Does.Not.Contain("<tag>"));
            var links = Regex.Matches(html, "href=\"([^\"]*/summary)\"").Select(match => HttpUtility.HtmlDecode(match.Groups[1].Value)).ToList();
            // Each run is linked from the results chart, the duration chart and the table.
            Assert.That(links.Count(link => link == "/team's%20reports/bdtest/report/O%27Brien/summary"), Is.EqualTo(3));
            Assert.That(links, Has.All.StartsWith("/team's%20reports/bdtest/report/"));
            Assert.That(html, Does.Not.Contain("attacker.example"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestCase(0, 0)]
    [TestCase(0, 4)]
    [TestCase(1, 0)]
    [TestCase(1, 4)]
    [TestCase(3, 0)]
    [TestCase(3, 4)]
    public async Task Trends_include_unfinished_scenarios_but_exclude_declared_tests(int unfinished, int declared)
    {
        var run = Run("unfinished", "nightly", Start, failed: 2);
        run.Counts = new Counts
        {
            Passed = 1, Failed = 2, Skipped = 3, Pending = 4, Inconclusive = 5, NotImplemented = 6,
            Total = 21 + unfinished, NotRun = unfinished + declared
        };

        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Trends.cshtml",
            new HistoryViewModel { AllRuns = [run], Runs = [run] });

        var bars = Regex.Match(html, "<div class=\"plot bars cur-last\">(.*?)</div>", RegexOptions.Singleline);
        var segments = XElement.Parse("<bars>" + bars.Groups[1].Value + "</bars>").Descendants("span").ToList();
        var heights = segments.Select(segment => double.Parse(
            Regex.Match(Required(segment.Attribute("style")).Value, @"height: ([\d.]+)%").Groups[1].Value,
            CultureInfo.InvariantCulture)).ToList();
        Assert.That(heights.Sum(), Is.EqualTo(100).Within(0.05), "All scenario results fill the chart, without declared tests.");
        Assert.That(heights[0], Is.EqualTo(100.0 / run.Counts.Total).Within(0.01));
        Assert.That(segments.Count(segment => (string?)segment.Attribute("class") == "bg-r"),
            Is.EqualTo(unfinished > 0 ? 2 : 1), "Pending and unfinished scenarios each have a segment.");

        var table = XElement.Parse(Regex.Match(html, "<tbody>(.*?)</tbody>", RegexOptions.Singleline).Value);
        var cells = table.Descendants("td").Select(cell => cell.Value).ToList();
        Assert.That(cells[2], Is.EqualTo("2"));
        Assert.That(cells[3], Is.EqualTo((18 + unfinished).ToString(CultureInfo.InvariantCulture)));
        Assert.That(cells[4], Is.EqualTo("1"));
    }

    [Test]
    public async Task Flaky_scenarios_link_to_their_runs_locally()
    {
        var reports = new List<BDTestOutputModel>
        {
            Report("O'Brien", "Failed", Start),
            Report("second", "Passed", Start.AddDays(1))
        };

        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/MultipleTestRunsFlakiness.cshtml", reports,
            pathBase: "/team's reports", host: "attacker.example", requestPath: "/bdtest/report/history/flaky");

        Assert.That(html, Does.Contain("1 scenario both passed and failed"));
        Assert.That(html, Does.Contain("&lt;flaky&gt; scenario"));
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(match => HttpUtility.HtmlDecode(match.Groups[1].Value))
            .Where(link => link.Contains("/report/"))
            .ToList();
        Assert.That(links, Does.Contain("/team's%20reports/bdtest/report/O%27Brien/scenario/scenario-O%27Brien"));
        Assert.That(links, Does.Contain("/team's%20reports/bdtest/report/O%27Brien/failures"));
        Assert.That(links, Does.Contain("/team's%20reports/bdtest/report/second/scenario/scenario-second"));
        Assert.That(html, Does.Not.Contain("attacker.example"));
    }

    [Test]
    public async Task Flaky_tab_compares_the_latest_runs_that_match_the_filters()
    {
        var runs = Enumerable.Range(0, 5).Select(i => Run($"nightly-{i}", "nightly", Start.AddDays(i), failed: 0))
            .Append(Run("other", "smoke", Start.AddDays(10), failed: 0))
            .ToList();
        var repository = new Repository(runs);
        var services = new ServiceCollection();
        services.AddBDTestReports();
        await using var provider = services.BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<BDTestController>(provider, repository);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.QueryString = new QueryString("?tag=nightly&range=all&compare=2");

        var result = (ViewResult)await controller.TestRunFlakiness(CancellationToken.None);

        Assert.That(result.ViewName, Is.EqualTo("MultipleTestRunsFlakiness"));
        Assert.That(repository.ReadIds, Is.EqualTo(new[] { "nightly-3", "nightly-4" }));
        Assert.That(((IEnumerable<BDTestOutputModel>)Required(result.Model)).Select(report => report.Id), Is.EqualTo(new[] { "nightly-3", "nightly-4" }));
        var history = (HistoryViewModel)Required(result.ViewData[nameof(HistoryViewModel)]);
        Assert.That(history.Tab, Is.EqualTo(HistoryViewModel.Flaky));
        Assert.That(history.Runs, Has.Count.EqualTo(5));
        Assert.That(history.ChosenRuns, Is.False);
    }

    private static TestRunSummary Run(string id, string tag, DateTime started, int failed)
    {
        var run = ReportJson.DeserializeObject<TestRunSummary>("{}");
        run.RecordId = id;
        run.Tag = tag;
        run.StartedAtDateTime = started;
        run.FinishedAtDateTime = started.AddSeconds(90 + failed * 10);
        run.Status = failed > 0 ? Status.Failed : Status.Passed;
        run.Counts.Failed = failed;
        run.Counts.Passed = 10 - failed;
        run.Counts.Total = 10;
        return run;
    }

    private static BDTestOutputModel Report(string id, string status, DateTime start) =>
        ReportJson.DeserializeObject<BDTestOutputModel>(ReportJson.SerializeObject(new
        {
            id,
            Scenarios = new[]
            {
                new
                {
                    Guid = "scenario-" + id, Status = status, StoryText = "As a tester\nI want <stable> tests\nSo that I trust them",
                    ScenarioText = "<flaky> scenario", StartTime = start, EndTime = start.AddSeconds(1),
                    Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
                }
            }
        }));

    private sealed class Repository(List<TestRunSummary> runs) : IDataRepository
    {
        public List<string> ReadIds { get; } = [];

        public Task<BDTestOutputModel?> GetData(string id, CancellationToken cancellationToken)
        {
            lock (ReadIds)
            {
                ReadIds.Add(id);
            }

            return Task.FromResult<BDTestOutputModel?>(new BDTestOutputModel { Id = id });
        }

        public Task<BDTestOutputModel?> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public Task<Stream?> OpenReadAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BDTest.Razor.Reports.Storage.RunIndexPage> ListAsync(BDTest.Razor.Reports.Storage.RunIndexQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<TestRunSummary>>(runs);
        public Task StoreData(string id, BDTestOutputModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteReport(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
