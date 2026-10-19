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
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class HistoryTests
{
    public static IEnumerable<string> GetValues() => Values;

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
    [MatrixDataSource]
    public async Task Filter_options_are_encoded_data([MatrixMethod<HistoryTests>(nameof(GetValues))] string value)
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
        await Assert.That(options).Count().IsEqualTo(4).Because("Tag, environment, branch and machine each offer the value once.");

        await Assert.That((options.Select(option => Required(option.Attribute("value")).Value)).All(item => Equals(item, value))).IsTrue();

        await Assert.That((options.Select(option => option.Value)).All(item => Equals(item, value))).IsTrue();
        await Assert.That(options.Count(option => option.Attribute("selected") != null)).IsEqualTo(1);
        await Assert.That(html).DoesNotContain("<script>alert(1)");
        await Assert.That(html).DoesNotContain("onmouseover=\"alert");
    }

    [Test]
    public async Task Comparison_tabs_keep_the_number_of_compared_runs()
    {
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_HistoryHeader.cshtml",
            new HistoryViewModel { Tab = HistoryViewModel.Flaky, Compare = 50, Filter = new RunFilter { Tag = "nightly" } });

        var tabs = Regex.Matches(html, "<a class=\"tab[^\"]*\" href=\"([^\"]+)\"")
            .Select(match => System.Web.HttpUtility.HtmlDecode(match.Groups[1].Value)).ToList();

        await Assert.That(tabs.Single(href => href.Contains("/history/timing"))).Contains("compare=50").And.Contains("tag=nightly");
        await Assert.That(tabs.Single(href => href.Contains("/history/flaky"))).Contains("compare=50");
        await Assert.That(tabs.Single(href => !href.Contains("/history/"))).DoesNotContain("compare=");
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


            await Assert.That(html).Contains("&quot;&#x96EA;&quot; &lt;tag&gt;").Or.Contains("&quot;雪&quot; &lt;tag&gt;");
            await Assert.That(html).DoesNotContain("<tag>");
            var links = Regex.Matches(html, "href=\"([^\"]*/summary)\"").Select(match => HttpUtility.HtmlDecode(match.Groups[1].Value)).ToList();
            // Each run is linked from the results chart, the duration chart and the table.
            await Assert.That(links.Count(link => link == "/team's%20reports/bdtest/report/O%27Brien/summary")).IsEqualTo(3);

            await Assert.That((links).All(item => item.StartsWith("/team's%20reports/bdtest/report/"))).IsTrue();
            await Assert.That(html).DoesNotContain("attacker.example");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(0, 4)]
    [Arguments(1, 0)]
    [Arguments(1, 4)]
    [Arguments(3, 0)]
    [Arguments(3, 4)]
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
        await Assert.That(heights.Sum()).IsEqualTo(100).Within(0.05).Because("All scenario results fill the chart, without declared tests.");
        await Assert.That(heights[0]).IsEqualTo(100.0 / Required(run.Counts).Total).Within(0.01);
        await Assert.That(segments.Count(segment => (string?)segment.Attribute("class") == "bg-r")).IsEqualTo(unfinished > 0 ? 2 : 1).Because("Pending and unfinished scenarios each have a segment.");

        var table = XElement.Parse(Regex.Match(html, "<tbody>(.*?)</tbody>", RegexOptions.Singleline).Value);
        var cells = table.Descendants("td").Select(cell => cell.Value).ToList();
        await Assert.That(cells[2]).IsEqualTo("2");
        await Assert.That(cells[3]).IsEqualTo((18 + unfinished).ToString(CultureInfo.InvariantCulture));
        await Assert.That(cells[4]).IsEqualTo("1");
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

        await Assert.That(html).Contains("1 scenario both passed and failed");
        await Assert.That(html).Contains("&lt;flaky&gt; scenario");
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(match => HttpUtility.HtmlDecode(match.Groups[1].Value))
            .Where(link => link.Contains("/report/"))
            .ToList();
        await Assert.That(links).Contains("/team's%20reports/bdtest/report/O%27Brien/scenario/scenario-O%27Brien");
        await Assert.That(links).Contains("/team's%20reports/bdtest/report/O%27Brien/failures");
        await Assert.That(links).Contains("/team's%20reports/bdtest/report/second/scenario/scenario-second");
        await Assert.That(html).DoesNotContain("attacker.example");
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

        await Assert.That(result.ViewName).IsEqualTo("MultipleTestRunsFlakiness");
        await Assert.That(repository.ReadIds).IsEquivalentTo(new[] { "nightly-3", "nightly-4" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(((IEnumerable<BDTestOutputModel>)Required(result.Model)).Select(report => report.Id)).IsEquivalentTo((new[] { "nightly-3", "nightly-4" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var history = (HistoryViewModel)Required(result.ViewData[nameof(HistoryViewModel)]);
        await Assert.That(history.Tab).IsEqualTo(HistoryViewModel.Flaky);
        await Assert.That(history.Runs).Count().IsEqualTo(5);
        await Assert.That(history.ChosenRuns).IsFalse();
    }

    private static TestRunSummary Run(string id, string tag, DateTime started, int failed)
    {
        var run = ReportJson.DeserializeObject<TestRunSummary>("{}");
        run.RecordId = id;
        run.Tag = tag;
        run.StartedAtDateTime = started;
        run.FinishedAtDateTime = started.AddSeconds(90 + failed * 10);
        run.Status = failed > 0 ? Status.Failed : Status.Passed;
        Required(run.Counts).Failed = failed;
        Required(run.Counts).Passed = 10 - failed;
        Required(run.Counts).Total = 10;
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
