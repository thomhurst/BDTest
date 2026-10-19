using Microsoft.Extensions.Options;
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
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ReportSidebarTests
{
    // Initialized by the asynchronous setup hook before each test.
    private WebApplication _application = null!;
    private HttpClient _client = null!;
    private readonly RecordingRepository _repository = new();
    private readonly RecordingLinksProvider _links = new();

    [Before(HookType.Test)]
    public async Task StartServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports(options => options.AllowAnonymousReports = true);
        builder.Services.AddSingleton<IDataRepository>(_repository);
        builder.Services.AddSingleton<IBDTestCustomSidebarLinksProvider>(_links);
        _application = builder.Build();
        _application.MapBDTestReports();
        await _application.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_application.Urls.Single()) };
    }

    [Before(HookType.Test)]
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

    [After(HookType.Test)]
    public async Task StopServer()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Retry_history_renders_attempt_status_duration_and_encoded_exceptions(bool retryHookFailed)
    {
        Required(_repository.Report).Scenarios = [ReportJson.DeserializeObject<Scenario>("""
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
            Required(Required(Required(_repository.Report).Scenarios).Single().Attempts).RemoveAt(1);

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        await Assert.That(html).Contains("Retry history");
        await Assert.That(html).Contains("Retries: 1");
        await Assert.That(html).Contains("Failed</span>");
        await Assert.That(html).Contains("title=\"00:00:00.1250000\"");
        if (!retryHookFailed)
        {
            await Assert.That(html).Contains("Passed</span>");
            await Assert.That(html).Contains("title=\"00:00:00.2500000\"");
        }
        await Assert.That(html).Contains("&lt;retry failure&gt;");
        await Assert.That(html).Contains("at FailingStep()");
        await Assert.That(html).DoesNotContain("<retry failure>");

        var list = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios");
        await Assert.That(list).Contains("<span class=\"tag\">retries 1</span>");
    }

    [Test]
    public async Task Story_links_filter_to_the_exact_story_and_preserve_it_when_searching()
    {
        const string story = "Story & <one> / 雪";
        Required(_repository.Report).Scenarios = Enumerable.Range(0, 3).Select(index =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
            {
                Guid = "s" + index, ScenarioText = "Scenario " + index,
                StoryText = index < 2 ? story : story + " other", Status = "Passed",
                Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
            }))).ToList();

        var detail = await _client.GetStringAsync("/bdtest/report/run-632/scenario/s0");
        var link = Regex.Match(detail, "href=\"([^\"]+)\">2 scenarios in this story</a>");
        await Assert.That(link.Success).IsTrue();
        var url = System.Net.WebUtility.HtmlDecode(link.Groups[1].Value);
        var list = await _client.GetStringAsync(url);

        await Assert.That(list).Contains("scenario-id=\"s0\"").And.Contains("scenario-id=\"s1\"").And.DoesNotContain("scenario-id=\"s2\"");
        await Assert.That(System.Net.WebUtility.HtmlDecode(list)).Contains("name=\"story\" value=\"" + story + "\"");

        var searched = await _client.GetStringAsync(url + "&q=Scenario%201");

        await Assert.That(searched).Contains("scenario-id=\"s1\"").And.DoesNotContain("scenario-id=\"s0\"");
        var clear = Regex.Match(list, "href=\"([^\"]+)\">Clear filters</a>");
        await Assert.That(clear.Success).IsTrue();
        var unfiltered = await _client.GetStringAsync(System.Net.WebUtility.HtmlDecode(clear.Groups[1].Value));
        await Assert.That(unfiltered).Contains("scenario-id=\"s2\"");
    }

    [Test]
    [Arguments("status", new[] { "b", "a", "c" })]
    [Arguments("name", new[] { "a", "b", "c" })]
    [Arguments("durationDescending", new[] { "c", "b", "a" })]
    [Arguments("durationAscending", new[] { "a", "b", "c" })]
    [Arguments("dateAscending", new[] { "a", "b", "c" })]
    [Arguments("dateDescending", new[] { "c", "b", "a" })]
    public async Task Stories_sort_scenarios_within_each_story(string order, string[] expected)
    {
        Required(_repository.Report).Scenarios = new[] { 2, 0, 1 }.Select(index =>
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
        await Assert.That(ids).IsEquivalentTo(expected, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Large_scenario_lists_are_paged_and_open_on_the_selected_scenarios_page()
    {
        Required(_repository.Report).Scenarios = Enumerable.Range(0, 150).Select(index => ReportJson.DeserializeObject<Scenario>(
            ReportJson.SerializeObject(new
            {
                Guid = $"s{index:000}", ScenarioText = $"Scenario {index:000}", Status = "Passed",
                Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>()
            }))).ToList();

        var first = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios?group=none&order=name");
        await Assert.That(Regex.Matches(first, "scenario-id=\"s\\d{3}\"")).Count().IsEqualTo(100);

        await Assert.That(first).Contains("scenario-id=\"s099\"").And.DoesNotContain("scenario-id=\"s100\"");
        await Assert.That(System.Net.WebUtility.HtmlDecode(first)).Contains("1–100 of 150 scenarios");

        var selected = await _client.GetStringAsync("/bdtest/report/run-632/all-scenarios?group=none&order=name&scenarioId=s130");

        await Assert.That(selected).Contains("scenario-id=\"s130\"").And.DoesNotContain("scenario-id=\"s099\"");
    }

    [Test]
    public async Task Structured_exceptions_render_all_branches_in_steps_scenarios_and_retries()
    {
        var exception = new ExceptionWrapper(new System.Reflection.TargetInvocationException(
            new AggregateException(new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>"))));
        exception.Data = new Dictionary<string, string> { ["<key>"] = "<value>" };
        exception.AsString = null; // The tree must not depend on the flattened diagnostic text.
        Required(_repository.Report).Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","RetryCount":1,"CustomTestInformation":[],
             "Steps":[{"StepText":"When failing","Status":"Failed"}],
             "Attempts":[{"Number":1,"Status":"Failed"}]}
            """)];
        var scenario = Required(Required(_repository.Report).Scenarios).Single();
        scenario.ExecutionException = ReportJson.DeserializeObject<ExceptionWrapper>(ReportJson.SerializeObject(exception));
        Required(scenario.Steps).Single().Exception = exception;
        Required(scenario.Attempts).Single().Exception = exception;

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");


        await Assert.That(html).Contains("System.InvalidOperationException").And.Contains("System.ArgumentException");

        await Assert.That(html).Contains("&lt;first cause&gt;").And.Contains("&lt;second cause&gt;");

        await Assert.That(html).Contains("&lt;key&gt;").And.Contains("&lt;value&gt;");

        await Assert.That(html).DoesNotContain("<first cause>").And.DoesNotContain("<key>");
        await Assert.That(html).Contains("exception-wrapper");
        await Assert.That(html.Split("<details class=\"exception-tree\" open>").Length - 1).IsEqualTo(3);
    }

    [Test]
    public async Task Top_defects_and_flakiness_group_all_aggregate_root_types()
    {
        Required(_repository.Report).Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","CustomTestInformation":[],"Steps":[],
             "StoryText":"story","ScenarioText":"scenario"}
            """)];
        Required(Required(_repository.Report).Scenarios).Single().ExecutionException = new ExceptionWrapper(new System.Reflection.TargetInvocationException(
            new AggregateException(new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>"))));

        var defects = await _client.GetStringAsync("/bdtest/report/run-632/top-defects");
        using var response = await _client.PostAsync("/bdtest/report/test-run-flakiness", new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("reportIds", "run-632,passing") }));
        response.EnsureSuccessStatusCode();
        var flakiness = await response.Content.ReadAsStringAsync();

        foreach (var html in new[] { defects, flakiness })
        {

            await Assert.That(html).Contains("System.InvalidOperationException").And.Contains("System.ArgumentException");

            await Assert.That(html).DoesNotContain("System.Reflection.TargetInvocationException").And.DoesNotContain("System.AggregateException");

            await Assert.That(html).Contains("&lt;first cause&gt;").And.Contains("&lt;second cause&gt;");
        }
    }

    [Test]
    public async Task Flakiness_preserves_distinct_messages_and_counts_each_failed_run_once()
    {
        Required(_repository.Report).Scenarios = [ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"scenario-632","Status":"Failed","CustomTestInformation":[],"Steps":[],
             "StoryText":"story","ScenarioText":"scenario"}
            """)];
        Required(Required(_repository.Report).Scenarios).Single().ExecutionException = new ExceptionWrapper(new AggregateException(
            new InvalidOperationException("<first cause>"), new InvalidOperationException("<second cause>"),
            new InvalidOperationException(" <first cause> ")));

        using var response = await _client.PostAsync("/bdtest/report/test-run-flakiness", new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("reportIds", "run-632,failed-again,passing") }));
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        await Assert.That(html).Contains("InvalidOperationException <span class=\"muted\">×2</span>");
        var rows = Regex.Matches(html, @"<tr>\s*<td[^>]*>InvalidOperationException</td>\s*<td[^>]*>(.*?)</td>\s*<td class=""num"">(\d+)</td>\s*</tr>");

        await Assert.That(rows.Select(row => row.Groups[1].Value)).IsEquivalentTo(new[] { "&lt;first cause&gt;", "&lt;second cause&gt;" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        await Assert.That((rows.Select(row => row.Groups[2].Value)).All(item => Equals(item, "2"))).IsTrue();
    }

    [Test]
    public async Task Old_reports_do_not_render_retry_history()
    {
        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");
        await Assert.That(html).DoesNotContain("Retry history");
    }

    [Test]
    public async Task Scenario_navigation_uses_the_loaded_report()
    {
        Required(Required(_repository.Report).Scenarios).Add(ReportJson.DeserializeObject<Scenario>("""
            { "Guid": "next-scenario", "Steps": [], "CustomTestInformation": [] }
            """));

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        await Assert.That(_repository.ReadTokens).Count().IsEqualTo(1);
        await Assert.That(html).Contains("href=\"/bdtest/report/run-632/scenario/next-scenario\" rel=\"next\"");
        await Assert.That(html).DoesNotContain("rel=\"prev\"");
    }

    [Test]
    [Arguments("stories")]
    [Arguments("all-scenarios")]
    public async Task Tag_filter_renders_only_matching_rows_and_keeps_all_tag_options(string page)
    {
        Required(_repository.Report).Scenarios = ReportJson.DeserializeObject<List<Scenario>>("""
            [
              { "Guid": "tagged", "Tags": ["smoke"], "Steps": [], "CustomTestInformation": [] },
              { "Guid": "other", "Tags": ["regression"], "Steps": [], "CustomTestInformation": [] },
              { "Guid": "legacy", "Steps": [], "CustomTestInformation": [] }
            ]
            """);

        var html = await _client.GetStringAsync($"/bdtest/report/run-632/{page}?filterByTag=SMOKE");

        await Assert.That(html).Contains("scenario-id=\"tagged\"");

        await Assert.That(html).DoesNotContain("scenario-id=\"other\"").And.DoesNotContain("scenario-id=\"legacy\"");
        await Assert.That(html).Contains("value=\"regression\"");
        await Assert.That(html).Contains("<span class=\"tag hide-phone\">smoke</span>");
        await Assert.That(_repository.ReadTokens).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("summary")]
    [Arguments("stories")]
    [Arguments("all-scenarios")]
    [Arguments("scenario/scenario-632")]
    [Arguments("scenario/missing")]
    [Arguments("timings")]
    [Arguments("top-defects")]
    [Arguments("warnings")]
    public async Task Report_pages_render_sidebar_from_the_single_loaded_report(string page)
    {
        var html = await _client.GetStringAsync("/bdtest/report/run-632/" + page);

        var compactPage = page is "summary" or "timings" or "top-defects" or "warnings";
        await Assert.That(_repository.ReadTokens).Count().IsEqualTo(compactPage ? 2 : 1);
        await Assert.That(_repository.ReadTokens.All(token => token.CanBeCanceled)).IsTrue();
        await Assert.That(_links.Reports).Count().IsEqualTo(1);
        await Assert.That(_links.Reports.Single()).IsSameReferenceAs(_repository.Report);
        await Assert.That(html).Contains("/bdtest/report/run-632/summary");
        await Assert.That(html).Contains("/bdtest/report/run-632/raw-json-data");
        await Assert.That(html).Contains("sidebar-environment");
        await Assert.That(html).Contains("sidebar-tag");
        await Assert.That(html).Contains("sidebar-branch");
        await Assert.That(html).Contains("Custom sidebar link");
    }

    [Test]
    [Arguments("summary")]
    [Arguments("stories")]
    [Arguments("all-scenarios")]
    [Arguments("scenario/missing")]
    public async Task Missing_reports_are_not_fetched_again_by_the_sidebar(string page)
    {
        _repository.Report = null;
        var html = await _client.GetStringAsync("/bdtest/report/missing/" + page);

        await Assert.That(_repository.ReadTokens).Count().IsEqualTo(1);
        await Assert.That(html).DoesNotContain("/bdtest/report/missing/raw-json-data");
        await Assert.That(_links.Reports).IsEquivalentTo(new BDTestOutputModel?[] { null }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("test-runs")]
    [Arguments("trends")]
    [Arguments("test-run-flakiness")]
    public async Task Global_pages_render_without_loading_a_report(string page)
    {
        var html = await _client.GetStringAsync("/bdtest/report/" + page);

        await Assert.That(_repository.ReadTokens).IsEmpty();
        await Assert.That(html).DoesNotContain("raw-json-data");
        await Assert.That(_links.Reports).IsEquivalentTo(new BDTestOutputModel?[] { null }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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

        await Assert.That(async () =>
            await controller.Summary("run-632", controller.HttpContext.RequestAborted)).ThrowsExactly<OperationCanceledException>();
        await Assert.That(_repository.ReadTokens).IsEquivalentTo(new[] { cancellation.Token }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private sealed class RecordingLinksProvider : IBDTestCustomSidebarLinksProvider
    {
        public List<BDTestOutputModel?> Reports { get; } = [];
        public IEnumerable<CustomLinkData> GetCustomLinks(BDTestOutputModel? report)
        {
            Reports.Add(report);
            return [new CustomLinkData("Custom sidebar link", new Uri("https://example.test"))];
        }
    }

    private sealed class RecordingRepository : IDataRepository
    {
        public BDTestOutputModel? Report { get; set; }
        public List<CancellationToken> ReadTokens { get; } = [];
        public Task<BDTestOutputModel?> GetData(string id, CancellationToken cancellationToken)
        {
            ReadTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (id == "passing")
            {
                var passed = Required(BDTest.Helpers.BDTestJsonHelper.Deserialize(BDTest.Helpers.BDTestJsonHelper.Serialize(Required(Report))));
                passed.Id = id;
                Required(passed.Scenarios).Single().Status = Status.Passed;
                return Task.FromResult<BDTestOutputModel?>(passed);
            }
            return Task.FromResult(Report);
        }
        public Task<BDTestOutputModel?> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public async Task<Stream?> OpenReadAsync(string id, CancellationToken cancellationToken)
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
