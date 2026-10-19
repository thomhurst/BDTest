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
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportNullableContractTests
{
    [Test]
    public async Task Stored_summary_defaults_and_explicit_nulls_are_preserved()
    {
        var empty = Required(JsonSerializer.Deserialize("{}", ReportJsonContext.Default.TestRunSummary));
        await Assert.That(empty.RecordId).IsNull();
        await Assert.That(empty.Tag).IsNull();
        await Assert.That(empty.Counts).IsNotNull();
        var emptyIndex = Required(JsonSerializer.Deserialize("{}", ReportJsonContext.Default.RunIndexEntry));
        await Assert.That(emptyIndex.RecordId).IsEmpty();
        await Assert.That(emptyIndex.Counts).IsNotNull();

        const string json = """
            {"RecordId":null,"Counts":null,"Tag":null,"Environment":null,
             "BranchName":null,"MachineName":null,"Version":null}
            """;
        var summary = Required(JsonSerializer.Deserialize(json, ReportJsonContext.Default.TestRunSummary));
        var index = Required(JsonSerializer.Deserialize(json, ReportJsonContext.Default.RunIndexEntry));
        await Assert.That(summary.Counts).IsNull();
        await Assert.That(index.Counts).IsNull();
        await Assert.That(index.RecordId).IsNull();
        await Assert.That(summary.ToRunIndexEntry().RecordId).IsNull();
        await Assert.That(index.ToTestRunSummary().RecordId).IsNull();
        await Assert.That(Required(summary.ToRunIndexEntry().Counts).Total).IsZero();
        await Assert.That(Required(index.ToTestRunSummary().Counts).Total).IsZero();
        foreach (var serialized in new[]
        {
            JsonSerializer.Serialize(summary, ReportJsonContext.Default.TestRunSummary),
            JsonSerializer.Serialize(index, ReportJsonContext.Default.RunIndexEntry)
        })
        {
            using var document = JsonDocument.Parse(serialized);
            foreach (var name in new[] { "RecordId", "Counts", "Tag", "Environment", "BranchName", "MachineName", "Version" })
                await Assert.That(document.RootElement.GetProperty(name).ValueKind).IsEqualTo(JsonValueKind.Null).Because(name);
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
        await Assert.That(options.UploadApiKey).IsNull();
        await Assert.That(options.ReportsAuthorizationPolicy).IsNull();
        await Assert.That(options.AdminAuthorizationPolicy).IsNull();
        var repository = provider.GetRequiredService<IDataRepository>();
        await Assert.That(await repository.GetData("missing", default)).IsNull();
        await Assert.That(await repository.GetDataForBulkRead("missing", default)).IsNull();
        await Assert.That(await repository.OpenReadAsync("missing", default)).IsNull();
        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
    }

    [Test]
    public async Task Legacy_store_bridge_preserves_a_json_null_root()
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(hostCache);
        var bridge = new LegacyDataStoreAdapter(store);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        await bridge.WriteAsync("null-report", input, default);
        await Assert.That(input.CanRead).IsTrue();
        await Assert.That(await store.GetTestData("null-report", default)).IsNull();
        await Assert.That(await bridge.OpenReadAsync("null-report", default)).IsNull();
    }

    [Test]
    public async Task Missing_query_values_and_optional_links_keep_their_null_semantics()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/reports";
        context.Request.QueryString = new QueryString("?tag=old&page=2");
        await Assert.That(context.Request.GetQueryParameter("missing")).IsNull();
        await Assert.That(context.GetLocalUrlWith(null, ("tag", null))).IsEqualTo("/reports?page=2");
        var link = new CustomLinkData(null, null);
        await Assert.That(link.Text).IsNull();
        await Assert.That(link.Url).IsNull();
        await Assert.That(ScenarioSearch.Matches(null, null)).IsFalse();
        await Assert.That(new Pager<int>([1, 2], null).ItemsForCurrentPage).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(new OutputMetrics(null).AnyWarnings).IsFalse();
    }

    [Test]
    public async Task Legacy_outline_examples_with_null_steps_and_values_render()
    {
        var scenario = ReportJson.DeserializeObject<Scenario>("""
            {"IsOutline":true,"Examples":[{"Steps":null,"ExampleValues":null,"Status":"Passed"}]}
            """);
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", scenario);

        await Assert.That(html).Contains("Example 1").And.Contains("1 of 1 passed");
        await Assert.That(Required(scenario.Examples)[0].Steps).IsNull();
        await Assert.That(scenario.Examples[0].ExampleValues).IsNull();
    }

    [Test]
    public async Task Legacy_summary_without_counts_renders_history()
    {
        var summary = Required(JsonSerializer.Deserialize("""
            {"RecordId":"legacy","Counts":null,"StartedAtDateTime":"2026-01-01T00:00:00Z"}
            """, ReportJsonContext.Default.TestRunSummary));
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Trends.cshtml",
            new HistoryViewModel { AllRuns = [summary], Runs = [summary] });
        await Assert.That(html).Contains("/bdtest/report/legacy/summary");
        await Assert.That(summary.Counts).IsNull();
    }
}
