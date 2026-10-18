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
        builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        builder.Services.AddSingleton<IDataRepository>(_repository);
        _application = builder.Build();
        _application.Services.GetRequiredService<BDTestReportServerOptions>().CustomSidebarLinksProvider = _links;
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
            Scenarios = [JsonConvert.DeserializeObject<Scenario>("""
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

    [Test]
    public async Task Scenario_navigation_uses_the_loaded_report()
    {
        _repository.Report.Scenarios.Add(JsonConvert.DeserializeObject<Scenario>("""
            { "Guid": "next-scenario", "Steps": [], "CustomTestInformation": [] }
            """));

        var html = await _client.GetStringAsync("/bdtest/report/run-632/scenario/scenario-632");

        Assert.That(_repository.ReadTokens, Has.Count.EqualTo(1));
        Assert.That(html, Does.Contain("/bdtest/report/run-632/scenario/next-scenario"));
        Assert.That(html, Does.Contain("Next Scenario"));
        Assert.That(html, Does.Not.Contain("Previous Scenario"));
    }

    [TestCase("stories")]
    [TestCase("all-scenarios")]
    public async Task Tag_filter_renders_only_matching_rows_and_keeps_all_tag_options(string page)
    {
        _repository.Report.Scenarios = JsonConvert.DeserializeObject<List<Scenario>>("""
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
        Assert.That(html, Does.Contain("badge bg-secondary mx-1\">smoke</span>"));
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
        Assert.That(html, Does.Contain("This Test Report"));
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
        Assert.That(html, Does.Not.Contain("This Test Report"));
        Assert.That(_links.Reports, Is.EqualTo(new BDTestOutputModel[] { null }));
    }

    [TestCase("test-runs")]
    [TestCase("trends")]
    [TestCase("test-run-flakiness")]
    public async Task Global_pages_render_without_loading_a_report(string page)
    {
        var html = await _client.GetStringAsync("/bdtest/report/" + page);

        Assert.That(_repository.ReadTokens, Is.Empty);
        Assert.That(html, Does.Not.Contain("This Test Report"));
        Assert.That(_links.Reports, Is.EqualTo(new BDTestOutputModel[] { null }));
    }

    [Test]
    public async Task Report_fetch_observes_request_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var options = _application.Services.GetRequiredService<BDTestReportServerOptions>();
        var controller = new BDTestController(_repository, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
            }
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
            return Task.FromResult(Report);
        }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreData(BDTestOutputModel model, string id) => throw new NotSupportedException();
        public Task DeleteReport(string id) => throw new NotSupportedException();
    }
}
