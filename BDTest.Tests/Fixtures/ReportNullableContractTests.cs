using System.Text;
using System.Text.Json;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Storage;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportNullableContractTests
{
    [Test]
    public void Stored_summary_defaults_and_explicit_nulls_are_preserved()
    {
        var empty = Required(JsonSerializer.Deserialize("{}", ReportJsonContext.Default.TestRunSummary));
        Assert.That(empty.RecordId, Is.Null);
        Assert.That(empty.Tag, Is.Null);
        Assert.That(empty.Counts, Is.Not.Null);
        var emptyIndex = Required(JsonSerializer.Deserialize("{}", ReportJsonContext.Default.RunIndexEntry));
        Assert.That(emptyIndex.RecordId, Is.Empty);
        Assert.That(emptyIndex.Counts, Is.Not.Null);

        const string json = """
            {"RecordId":null,"Counts":null,"Tag":null,"Environment":null,
             "BranchName":null,"MachineName":null,"Version":null}
            """;
        var summary = Required(JsonSerializer.Deserialize(json, ReportJsonContext.Default.TestRunSummary));
        var index = Required(JsonSerializer.Deserialize(json, ReportJsonContext.Default.RunIndexEntry));
        Assert.That(summary.Counts, Is.Null);
        Assert.That(index.Counts, Is.Null);
        Assert.That(index.RecordId, Is.Null);
        Assert.That(summary.ToRunIndexEntry().RecordId, Is.Null);
        Assert.That(index.ToTestRunSummary().RecordId, Is.Null);
        Assert.That(Required(summary.ToRunIndexEntry().Counts).Total, Is.Zero);
        Assert.That(Required(index.ToTestRunSummary().Counts).Total, Is.Zero);
        foreach (var serialized in new[]
        {
            JsonSerializer.Serialize(summary, ReportJsonContext.Default.TestRunSummary),
            JsonSerializer.Serialize(index, ReportJsonContext.Default.RunIndexEntry)
        })
        {
            using var document = JsonDocument.Parse(serialized);
            foreach (var name in new[] { "RecordId", "Counts", "Tag", "Environment", "BranchName", "MachineName", "Version" })
                Assert.That(document.RootElement.GetProperty(name).ValueKind, Is.EqualTo(JsonValueKind.Null), name);
        }
    }

    [Test]
    public async Task Missing_storage_and_optional_host_configuration_remain_supported()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports(configure: null);
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        Assert.That(options.UploadApiKey, Is.Null);
        Assert.That(options.ReportsAuthorizationPolicy, Is.Null);
        Assert.That(options.AdminAuthorizationPolicy, Is.Null);
        var repository = provider.GetRequiredService<IDataRepository>();
        Assert.That(await repository.GetData("missing", default), Is.Null);
        Assert.That(await repository.GetDataForBulkRead("missing", default), Is.Null);
        Assert.That(await repository.OpenReadAsync("missing", default), Is.Null);
        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
    }

    [Test]
    public async Task Legacy_store_bridge_preserves_a_json_null_root()
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(hostCache);
        var bridge = new LegacyDataStoreAdapter(store);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        await bridge.WriteAsync("null-report", input, default);
        Assert.That(input.CanRead, Is.True);
        Assert.That(await store.GetTestData("null-report", default), Is.Null);
        Assert.That(await bridge.OpenReadAsync("null-report", default), Is.Null);
    }

    [Test]
    public void Missing_query_values_and_optional_links_keep_their_null_semantics()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/reports";
        context.Request.QueryString = new QueryString("?tag=old&page=2");
        Assert.That(context.Request.GetQueryParameter("missing"), Is.Null);
        Assert.That(context.GetLocalUrlWith(null, ("tag", null)), Is.EqualTo("/reports?page=2"));
        var link = new CustomLinkData(null, null);
        Assert.That(link.Text, Is.Null);
        Assert.That(link.Url, Is.Null);
        Assert.That(ScenarioSearch.Matches(null, null), Is.False);
        Assert.That(new Pager<int>([1, 2], null).ItemsForCurrentPage, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(new OutputMetrics(null).AnyWarnings, Is.False);
    }

    [Test]
    public async Task Legacy_outline_examples_with_null_steps_and_values_render()
    {
        var scenario = ReportJson.DeserializeObject<Scenario>("""
            {"IsOutline":true,"Examples":[{"Steps":null,"ExampleValues":null,"Status":"Passed"}]}
            """);
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", scenario);
        Assert.That(html, Does.Contain("Example 1").And.Contain("1 of 1 passed"));
        Assert.That(Required(scenario.Examples)[0].Steps, Is.Null);
        Assert.That(scenario.Examples[0].ExampleValues, Is.Null);
    }

    [Test]
    public async Task Legacy_summary_without_counts_renders_history()
    {
        var summary = Required(JsonSerializer.Deserialize("""
            {"RecordId":"legacy","Counts":null,"StartedAtDateTime":"2026-01-01T00:00:00Z"}
            """, ReportJsonContext.Default.TestRunSummary));
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Trends.cshtml",
            new HistoryViewModel { AllRuns = [summary], Runs = [summary] });
        Assert.That(html, Does.Contain("/bdtest/report/legacy/summary"));
        Assert.That(summary.Counts, Is.Null);
    }
}
