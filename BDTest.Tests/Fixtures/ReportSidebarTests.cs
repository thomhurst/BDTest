using System.Text.RegularExpressions;
using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportSidebarTests
{
    private WebApplication _application;
    private HttpClient _client;
    private readonly RecordingRepository _repository = new();
    private readonly RecordingLinksProvider _links = new();

    [OneTimeSetUp]
    public async Task StartServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports();
        builder.Services.AddSingleton<IDataRepository>(_repository);
        builder.Services.AddSingleton<IBDTestCustomSidebarLinksProvider>(_links);
        _application = builder.Build();
        _application.MapControllers();
        await _application.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_application.Urls.Single()) };
    }

    [SetUp]
    public void Reset()
    {
        _repository.ReadTokens.Clear();
        _repository.Report = new BDTestOutputModel
        {
            Id = "run-632",
            Environment = "sidebar-environment",
            Tag = "sidebar-tag",
            BranchName = "sidebar-branch",
            Scenarios = [ReportJson.DeserializeObject<Scenario>("""
                { "Guid": "scenario-632", "Steps": [], "CustomTestInformation": [] }
                """)]
        };
        _links.Reports.Clear();
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Retry_history_renders_attempt_status_duration_and_encoded_exceptions(bool retryHookFailed)
    {
        _repository.Report.Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {
              "Guid": "scenario-632", "Status": "Passed", "RetryCount": 1,
              "Steps": [], "CustomTestInformation": [],
              "Attempts": [
                { "Number": 1, "Status": "Failed", "TimeTaken": "0.00:00:00:1250000",
                  "Exception": { "TypeName": "InvalidOperationException", "Message": "<retry failure>",
                    "AsString": "InvalidOperationException: <retry failure>\n   at FailingStep()" } },
                { "Number": 2, "Status": "Passed", "TimeTaken": "0.00:00:00:2500000" }
              ]
            }
            """)];

        if (retryHookFailed)
            _repository.Report.Scenarios.Single().Attempts.RemoveAt(1);

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        Assert.That(html, Does.Contain("Retry history"));
        Assert.That(html, Does.Contain("Retries: 1"));
        Assert.That(html, Does.Contain("Failed</span>"));
        Assert.That(html, Does.Contain("title=\"00:00:00.1250000\""));
        if (!retryHookFailed)
        {
            Assert.That(html, Does.Contain("Passed</span>"));
            Assert.That(html, Does.Contain("title=\"00:00:00.2500000\""));
        }
        Assert.That(html, Does.Contain("&lt;retry failure&gt;"));
        Assert.That(html, Does.Contain("at FailingStep()"));
        Assert.That(html, Does.Not.Contain("<retry failure>"));

        var list = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios");
        Assert.That(list, Does.Contain("<span class=\"tag\">retries 1</span>"));
    }

    [Test]
    public async Task Story_links_filter_to_the_exact_story_and_preserve_it_when_searching()
    {
        const string story = "Story & <one> / 雪";
        _repository.Report.Scenarios = Enumerable.Range(0, 3).Select(index =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
            {
                Guid = "s" + index, ScenarioText = "Scenario " + index,
                StoryText = index < 2 ? story : story + " other", Status = "Passed",
                Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
            }))).ToList();

        var detail = await _client.GetStringAsync("/bdtest/report/run-632/scenario/s0");
        var link = Regex.Match(detail, "href=\"([^\"]+)\">2 scenarios in this story</a>");
        Assert.That(link.Success, Is.True);
        var url = System.Net.WebUtility.HtmlDecode(link.Groups[1].Value);
        var list = await _client.GetStringAsync(url);
        Assert.That(list, Does.Contain("scenario-id=\"s0\"").And.Contain("scenario-id=\"s1\"")
            .And.Not.Contain("scenario-id=\"s2\""));
        Assert.That(System.Net.WebUtility.HtmlDecode(list),
            Does.Contain("name=\"story\" value=\"" + story + "\""));

        var searched = await _client.GetStringAsync(url + "&q=Scenario%201");
        Assert.That(searched, Does.Contain("scenario-id=\"s1\"").And.Not.Contain("scenario-id=\"s0\""));
        var clear = Regex.Match(list, "href=\"([^\"]+)\">Clear filters</a>");
        Assert.That(clear.Success, Is.True);
        var unfiltered = await _client.GetStringAsync(System.Net.WebUtility.HtmlDecode(clear.Groups[1].Value));
        Assert.That(unfiltered, Does.Contain("scenario-id=\"s2\""));
    }

    [TestCase("status", new[] { "b", "a", "c" })]
    [TestCase("name", new[] { "a", "b", "c" })]
    [TestCase("durationDescending", new[] { "c", "b", "a" })]
    [TestCase("durationAscending", new[] { "a", "b", "c" })]
    [TestCase("dateAscending", new[] { "a", "b", "c" })]
    [TestCase("dateDescending", new[] { "c", "b", "a" })]
    public async Task Stories_sort_scenarios_within_each_story(string order, string[] expected)
    {
        _repository.Report.Scenarios = new[] { 2, 0, 1 }.Select(index =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
            {
                Guid = ((char)('a' + index)).ToString(), ScenarioText = ((char)('A' + index)).ToString(),
                StoryText = "Shared story", Status = index == 1 ? "Failed" : "Passed",
                StartTime = new DateTime(2026, 10, 18, 12, index, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 10, 18, 12, index, index + 1, DateTimeKind.Utc),
                TimeTaken = TimeSpan.FromSeconds(index + 1).ToString(@"d\.hh\:mm\:ss\:fffffff"),
                Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
            }))).ToList();

        var html = await _client.GetStringAsync("/bdtest/report/run-632/stories?order=" + order);
        var ids = Regex.Matches(html, "scenario-id=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value);
        Assert.That(ids, Is.EqualTo(expected));
    }

    [Test]
    public async Task Large_scenario_lists_are_paged_and_open_on_the_selected_scenarios_page()
    {
        _repository.Report.Scenarios = Enumerable.Range(0, 150).Select(index => ReportJson.DeserializeObject<Scenario>(
            ReportJson.SerializeObject(new
            {
                Guid = $"s{index:000}", ScenarioText = $"Scenario {index:000}", Status = "Passed",
                Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
            }))).ToList();

        var first = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios?group=none&order=name");
        Assert.That(Regex.Matches(first, "scenario-id=\"s\\d{3}\""), Has.Count.EqualTo(100));
        Assert.That(first, Does.Contain("scenario-id=\"s099\"").And.Not.Contain("scenario-id=\"s100\""));
        Assert.That(System.Net.WebUtility.HtmlDecode(first), Does.Contain("1–100 of 150 scenarios"));

        var selected = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios?group=none&order=name&scenarioId=s130");
        Assert.That(selected, Does.Contain("scenario-id=\"s130\"").And.Not.Contain("scenario-id=\"s099\""));
    }

    [Test]
    public async Task Structured_exceptions_render_all_branches_in_steps_scenarios_and_retries()
    {
        var exception = new ExceptionWrapper(new System.Reflection.TargetInvocationException(
            new AggregateException(new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>"))));
        exception.Data = new Dictionary<string, string> { ["<key>"] = "<value>" };
        exception.AsString = null; // The tree must not depend on the flattened diagnostic text.
        _repository.Report.Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","RetryCount":1,"CustomTestInformation":[],
             "Steps":[{"StepText":"When failing","Status":"Failed"}],
             "Attempts":[{"Number":1,"Status":"Failed"}]}
            """)];
        var scenario = _repository.Report.Scenarios.Single();
        scenario.ExecutionException = ReportJson.DeserializeObject<ExceptionWrapper>(ReportJson.SerializeObject(exception));
        scenario.Steps.Single().Exception = exception;
        scenario.Attempts.Single().Exception = exception;

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        Assert.That(html, Does.Contain("System.InvalidOperationException").And.Contain("System.ArgumentException"));
        Assert.That(html, Does.Contain("&lt;first cause&gt;").And.Contain("&lt;second cause&gt;"));
        Assert.That(html, Does.Contain("&lt;key&gt;").And.Contain("&lt;value&gt;"));
        Assert.That(html, Does.Not.Contain("<first cause>").And.Not.Contain("<key>"));
        Assert.That(html, Does.Contain("exception-wrapper"));
        Assert.That(html.Split("<details class=\"exception-tree\" open>").Length - 1, Is.EqualTo(3));
    }

    [Test]
    public async Task Top_defects_and_flakiness_group_all_aggregate_root_types()
    {
        _repository.Report.Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","CustomTestInformation":[],"Steps":[],
             "StoryText":"story","ScenarioText":"scenario"}
            """)];
        _repository.Report.Scenarios.Single().ExecutionException = new ExceptionWrapper(new System.Reflection.TargetInvocationException(
            new AggregateException(new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>"))));

        var defects = await _client.GetStringAsync("/bdtest/report/run-632/top-defects");
        using var response = await _client.PostAsync("/bdtest/report/test-run-flakiness", new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("reportIds", "run-632,passing") }));
        response.EnsureSuccessStatusCode();
        var flakiness = await response.Content.ReadAsStringAsync();

        foreach (var html in new[] { defects, flakiness })
        {
            Assert.That(html, Does.Contain("System.InvalidOperationException").And.Contain("System.ArgumentException"));
            Assert.That(html, Does.Not.Contain("System.Reflection.TargetInvocationException").And.Not.Contain("System.AggregateException"));
            Assert.That(html, Does.Contain("&lt;first cause&gt;").And.Contain("&lt;second cause&gt;"));
        }
    }

    [Test]
    public async Task Flakiness_preserves_distinct_messages_and_counts_each_failed_run_once()
    {
        _repository.Report.Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","CustomTestInformation":[],"Steps":[],
             "StoryText":"story","ScenarioText":"scenario"}
            """)];
        _repository.Report.Scenarios.Single().ExecutionException = new ExceptionWrapper(new AggregateException(
            new InvalidOperationException("<first cause>"), new InvalidOperationException("<second cause>"),
            new InvalidOperationException(" <first cause> ")));

        using var response = await _client.PostAsync("/bdtest/report/test-run-flakiness", new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("reportIds", "run-632,failed-again,passing") }));
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.That(html, Does.Contain("InvalidOperationException <span class=\"muted\">×2</span>"));
        var rows = Regex.Matches(html, @"<tr>\s*<td[^>]*>InvalidOperationException</td>\s*<td[^>]*>(.*?)</td>\s*<td class=""num"">(\d+)</td>\s*</tr>");
        Assert.That(rows.Select(row => row.Groups[1].Value),
            Is.EquivalentTo(new[] { "&lt;first cause&gt;", "&lt;second cause&gt;" }));
        Assert.That(rows.Select(row => row.Groups[2].Value), Is.All.EqualTo("2"));
    }

    [Test]
    public async Task Old_reports_do_not_render_retry_history()
    {
        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");
        Assert.That(html, Does.Not.Contain("Retry history"));
    }

    [Test]
    public async Task Scenario_navigation_uses_the_loaded_report()
    {
        _repository.Report.Scenarios.Add(ReportJson.DeserializeObject<Scenario>("""
            { "Guid": "next-scenario", "Steps": [], "CustomTestInformation": [] }
            """));

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        Assert.That(_repository.ReadTokens, Has.Count.EqualTo(1));
        Assert.That(html, Does.Contain("href=\"/bdtest/report/run-632/scenario/next-scenario\" rel=\"next\""));
        Assert.That(html, Does.Not.Contain("rel=\"prev\""));
    }

    [TestCase("stories")]
    [TestCase("all-scenarios")]
    public async Task Tag_filter_renders_only_matching_rows_and_keeps_all_tag_options(string page)
    {
        _repository.Report.Scenarios = ReportJson.DeserializeObject<List<Scenario>>("""
            [
              { "Guid": "tagged", "Tags": ["smoke"], "Steps": [], "CustomTestInformation": [] },
              { "Guid": "other", "Tags": ["regression"], "Steps": [], "CustomTestInformation": [] },
              { "Guid": "legacy", "Steps": [], "CustomTestInformation": [] }
            ]
            """);

        var html = await _client.GetStringAsync($"/bdtest/report/run-632/{page}?filterByTag=SMOKE");

        Assert.That(html, Does.Contain("scenario-id=\"tagged\""));
        Assert.That(html, Does.Not.Contain("scenario-id=\"other\"").And.Not.Contain("scenario-id=\"legacy\""));
        Assert.That(html, Does.Contain("value=\"regression\""));
        Assert.That(html, Does.Contain("<span class=\"tag hide-phone\">smoke</span>"));
        Assert.That(_repository.ReadTokens, Has.Count.EqualTo(1));
    }

    [TestCase("summary")]
    [TestCase("stories")]
    [TestCase("all-scenarios")]
    [TestCase("scenario/scenario-632")]
    [TestCase("scenario/missing")]
    [TestCase("timings")]
    [TestCase("top-defects")]
    [TestCase("warnings")]
    public async Task Report_pages_render_sidebar_from_the_single_loaded_report(string page)
    {
        var html = await _client.GetStringAsync("/bdtest/report/run-632/" + page);

        Assert.That(_repository.ReadTokens, Has.Count.EqualTo(1));
        Assert.That(_repository.ReadTokens.Single().CanBeCanceled, Is.True);
        Assert.That(_links.Reports, Has.Count.EqualTo(1));
        Assert.That(_links.Reports.Single(), Is.SameAs(_repository.Report));
        Assert.That(html, Does.Contain("/bdtest/report/run-632/summary"));
        Assert.That(html, Does.Contain("/bdtest/report/run-632/raw-json-data"));
        Assert.That(html, Does.Contain("sidebar-environment"));
        Assert.That(html, Does.Contain("sidebar-tag"));
        Assert.That(html, Does.Contain("sidebar-branch"));
        Assert.That(html, Does.Contain("Custom sidebar link"));
    }

    [TestCase("summary")]
    [TestCase("stories")]
    [TestCase("all-scenarios")]
    [TestCase("scenario/missing")]
    public async Task Missing_reports_are_not_fetched_again_by_the_sidebar(string page)
    {
        _repository.Report = null;
        var html = await _client.GetStringAsync("/bdtest/report/missing/" + page);

        Assert.That(_repository.ReadTokens, Has.Count.EqualTo(1));
        Assert.That(html, Does.Not.Contain("/bdtest/report/missing/raw-json-data"));
        Assert.That(_links.Reports, Is.EqualTo(new BDTestOutputModel[] { null }));
    }

    [TestCase("test-runs")]
    [TestCase("trends")]
    [TestCase("test-run-flakiness")]
    public async Task Global_pages_render_without_loading_a_report(string page)
    {
        var html = await _client.GetStringAsync("/bdtest/report/" + page);

        Assert.That(_repository.ReadTokens, Is.Empty);
        Assert.That(html, Does.Not.Contain("raw-json-data"));
        Assert.That(_links.Reports, Is.EqualTo(new BDTestOutputModel[] { null }));
    }

    [Test]
    public async Task Report_fetch_observes_request_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var controller = ActivatorUtilities.CreateInstance<BDTestController>(_application.Services, _repository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
        };
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await controller.Summary("run-632", controller.HttpContext.RequestAborted));
        Assert.That(_repository.ReadTokens, Is.EqualTo(new[] { cancellation.Token }));
    }

    private sealed class RecordingLinksProvider : IBDTestCustomSidebarLinksProvider
    {
        public List<BDTestOutputModel> Reports { get; } = [];
        public IEnumerable<CustomLinkData> GetCustomLinks(BDTestOutputModel report)
        {
            Reports.Add(report);
            return [new CustomLinkData("Custom sidebar link", new Uri("https://example.test"))];
        }
    }

    private sealed class RecordingRepository : IDataRepository
    {
        public BDTestOutputModel Report { get; set; }
        public List<CancellationToken> ReadTokens { get; } = [];
        public Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken)
        {
            ReadTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (id == "passing")
            {
                var passed = BDTest.Helpers.BDTestJsonHelper.Deserialize(BDTest.Helpers.BDTestJsonHelper.Serialize(Report));
                passed.Id = id;
                passed.Scenarios.Single().Status = Status.Passed;
                return Task.FromResult(passed);
            }
            return Task.FromResult(Report);
        }
        public Task<BDTestOutputModel> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken)
        {
            var model = await GetData(id, cancellationToken);
            return model == null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BDTest.Helpers.BDTestJsonHelper.Serialize(model)));
        }
        public Task<BDTest.Razor.Reports.Storage.RunIndexPage> ListAsync(BDTest.Razor.Reports.Storage.RunIndexQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreData(string id, BDTestOutputModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteReport(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
