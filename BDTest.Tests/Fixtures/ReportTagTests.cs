using System.Text.RegularExpressions;
using System.Web;
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
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportTagTests
{
    [TestCase(false, "?filterByTag=SMOKE", new[] { "smoke-passed", "smoke-failed" })]
    [TestCase(false, "?filterByTag=smoke&filterByStatus=Passed", new[] { "smoke-passed" })]
    [TestCase(false, "?filterByTag=missing", new string[0])]
    [TestCase(false, "?filterByTag=", new[] { "smoke-passed", "smoke-failed", "other", "legacy" })]
    [TestCase(true, "?filterByTag=SMOKE", new[] { "smoke-passed", "smoke-failed" })]
    [TestCase(true, "?filterByTag=smoke&filterByStatus=Failed", new[] { "smoke-failed" })]
    [TestCase(true, "?filterByTag=smoke&filterByStatus=Passed", new[] { "smoke-passed" })]
    [TestCase(true, "?filterByTag=smoke&filterByStatus=Passed,Failed", new[] { "smoke-passed", "smoke-failed" })]
    [TestCase(true, "?filterByTag=missing", new string[0])]
    [TestCase(true, "", new[] { "smoke-passed", "smoke-failed", "other", "legacy" })]
    public async Task Tag_filter_runs_before_grouping_and_combines_with_status(bool stories, string query, string[] expected)
    {
        var repository = new Repository();
        var original = ReportJson.SerializeObject(repository.Report);
        var services = new ServiceCollection();
        services.AddBDTestReports();
        using var provider = services.BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<BDTestController>(provider, repository);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.QueryString = new QueryString(query);

        var result = (ViewResult)(stories
            ? await controller.Stories("run", CancellationToken.None)
            : await controller.AllScenarios("run", CancellationToken.None));
        var groups = (IGrouping<string, Scenario>[])result.Model;

        Assert.That(groups.SelectMany(group => group).Select(scenario => scenario.Guid), Is.EquivalentTo(expected));
        Assert.That(ReportJson.SerializeObject(repository.Report), Is.EqualTo(original), "Filtering must not mutate cached reports.");
    }

    [Test]
    public async Task Tag_dropdown_encodes_tags_and_preserves_other_filters_without_the_old_page()
    {
        const string tag = "O'Brien & <smoke> 雪";
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioToolbar.cshtml",
            new ScenarioToolbarViewModel { Tags = new[] { tag, "other" } }, query: "?filterByTag=" + Uri.EscapeDataString(tag) + "&filterByStatus=Passed&orderBy=Name&page=5",
            pathBase: "/reports", host: "attacker.example");

        Assert.That(html, Does.Contain("name=\"filterByTag\""));
        Assert.That(html, Does.Contain("name=\"filterByStatus\" value=\"Passed\""));
        Assert.That(html, Does.Contain("name=\"orderBy\" value=\"Name\""));
        Assert.That(html, Does.Not.Contain("name=\"page\""));
        Assert.That(html, Does.Contain("action=\"/reports/bdtest/"));
        Assert.That(html, Does.Not.Contain("attacker.example").And.Not.Contain("<smoke>"));
        var selected = Regex.Match(html, "<option[^>]*value=\"([^\"]+)\"[^>]*selected[^>]*>");
        Assert.That(selected.Success, Is.True);
        Assert.That(HttpUtility.HtmlDecode(selected.Groups[1].Value), Is.EqualTo(tag));
    }

    [Test]
    public async Task Tag_badges_encode_uploaded_text()
    {
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "tagged", Tags = new[] { "<script>alert(1)</script>" }, Steps = Array.Empty<object>()
        }));
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioPanel.cshtml",
            new ScenarioModalViewModel { Scenario = scenario });
        Assert.That(html, Does.Not.Contain("<script>"));
        Assert.That(HttpUtility.HtmlDecode(html), Does.Contain("<span class=\"tag\"><script>alert(1)</script></span>"));
    }

    private sealed class Repository : IDataRepository
    {
        public BDTestOutputModel Report { get; } = new()
        {
            Scenarios =
            [
                Create("smoke-passed", "Passed", "smoke"),
                Create("smoke-failed", "Failed", "smoke"),
                Create("other", "Passed", "regression"),
                Create("legacy", "Passed", null)
            ]
        };

        private static Scenario Create(string id, string status, string tag) =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
            {
                Guid = id, Status = status, Tags = tag == null ? null : new[] { tag },
                ScenarioText = "Shared scenario", StoryText = "Shared story", Steps = Array.Empty<object>()
            }));

        public Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken) => Task.FromResult(Report);
        public Task<BDTestOutputModel> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken)
        {
            var model = await GetData(id, cancellationToken);
            return model == null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BDTest.Helpers.BDTestJsonHelper.Serialize(model)));
        }
        public Task<BDTest.Razor.Reports.Storage.RunIndexPage> ListAsync(BDTest.Razor.Reports.Storage.RunIndexQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StoreData(string id, BDTestOutputModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteReport(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
