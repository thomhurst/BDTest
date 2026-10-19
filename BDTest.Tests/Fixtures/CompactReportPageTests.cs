using System.Collections;
using System.Text;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Storage;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CompactReportPageTests
{
    private static ServiceProvider Services(long pageCapacity = 64 * 1024 * 1024, TimeSpan? retention = null,
        long reportCapacity = 64 * 1024 * 1024)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports(options =>
        {
            options.ReportPageCacheSizeLimitBytes = pageCapacity;
            options.ReportCacheSizeLimitBytes = reportCapacity;
            options.ReportCacheRetention = retention ?? TimeSpan.FromHours(8);
        });
        return services.BuildServiceProvider();
    }

    private static BDTestOutputModel Report(int count = 14, string id = "compact") => new()
    {
        Id = id, Tag = "smoke", Environment = "CI", BranchName = "main",
        Scenarios = Enumerable.Range(0, count).Select(i => ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "scenario-" + i, ScenarioText = "Scenario " + i, StoryText = "Story " + i % 3,
            Status = Enum.GetValues<Status>()[i % Enum.GetValues<Status>().Length].ToString(),
            TimeTaken = TimeSpan.FromTicks((i + 1) * 1000L).ToString(@"d\.hh\:mm\:ss\:fffffff"),
            StartTime = "2026-10-01T12:00:00Z", EndTime = "2026-10-01T12:01:00Z",
            Tags = new[] { "smoke" }, RetryCount = 1,
            Steps = new[] { new { StepText = "Then <it works>", Status = "Failed",
                Exception = new { TypeName = "AssertionException", Message = "Expected <success>", AsString = "trace <encoded>" } } }
        }))).ToList(),
        NotRun = ReportJson.DeserializeObject<List<ScenarioDefinitionSummary>>("""
            [{ "ScenarioText": "Zebra", "StoryText": "A", "CallerFile": "C:\\src\\Test.cs", "CallerMember": "Z" },
             { "ScenarioText": "Alpha", "StoryText": "A", "CallerFile": "/src/Other.cs", "CallerMember": "A" }]
            """)
    };

    [Test]
    public void Projections_preserve_statuses_groupings_order_timings_and_warnings()
    {
        var report = Report(140);
        var scenarios = Required(report.Scenarios);
        var pages = ReportPageModels.Create(report);
        var layout = pages.Summary.Layout;
        Assert.That(layout.ScenarioCount, Is.EqualTo(140));
        Assert.That(layout.Status, Is.EqualTo(scenarios.GetTotalStatus()));
        foreach (var status in Enum.GetValues<Status>())
            Assert.That(layout.Counts[status], Is.EqualTo(scenarios.Count(s => s.Status == status)));
        foreach (var grouping in new[] { "location", "type", "step", "unknown" })
        {
            var expected = FailureGroup.Build(scenarios, FailureGroup.Normalize(grouping));
            var actual = pages.TopDefects.ForGrouping(grouping);
            Assert.That(actual.Select(g => (g.Title, g.Location, g.TypeName)), Is.EqualTo(expected.Select(g => (g.Title, g.Location, g.TypeName))));
            for (var i = 0; i < expected.Count; i++)
                Assert.That(actual[i].Scenarios.Select(s => s.Guid), Is.EqualTo(expected[i].Scenarios.Select(s => s.Guid)));
        }
        var timed = scenarios.Where(s => s.Status is not (Status.Skipped or Status.NotRun)).OrderBy(s => s.TimeTaken).ToList();
        Assert.That(pages.Timings.Fastest.Select(s => s.Guid), Is.EqualTo(timed.Take(10).Select(s => s.Guid)));
        Assert.That(pages.Timings.Slowest.Select(s => s.Guid), Is.EqualTo(timed.AsEnumerable().Reverse().Take(10).Select(s => s.Guid)));
        Assert.That(pages.Timings.Median, Is.EqualTo(timed[(int)Math.Ceiling(timed.Count * .5) - 1].TimeTaken));
        Assert.That(pages.Timings.Percentile95, Is.EqualTo(timed[(int)Math.Ceiling(timed.Count * .95) - 1].TimeTaken));
        Assert.That(pages.Summary.Others, Has.Count.EqualTo(10));
        Assert.That(pages.Summary.OtherCount, Is.EqualTo(scenarios.Count(s => s.Status is not (Status.Passed or Status.Failed))));
        Assert.That(pages.Summary.Failures.All(g => g.Scenarios.Count <= 5 && g.Trace == null), Is.True);
        Assert.That(pages.Warnings.NotRun.Select(row => row.Scenario), Is.EqualTo(new[] { "Alpha", "Zebra" }));
        AssertNoSourceGraph(pages, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    [Test]
    public void Full_data_layout_groups_failures_only_on_first_access()
    {
        var report = Report();
        var scenarios = Required(report.Scenarios);
        foreach (var scenario in scenarios) scenario.Status = Status.Passed;
        var layout = ReportLayoutViewModel.Create(report);

        // A failure introduced before first access must be included, proving creation did not group.
        scenarios[0].Status = Status.Failed;
        Assert.That(layout.FailureCauses, Is.EqualTo(1));
        scenarios[0].Status = Status.Passed;
        Assert.That(layout.FailureCauses, Is.EqualTo(1), "The computed count is reused.");
    }

    [Test]
    public void Cached_layout_keeps_its_precomputed_zero_failure_count()
    {
        var report = Report();
        var scenarios = Required(report.Scenarios);
        foreach (var scenario in scenarios) scenario.Status = Status.Passed;
        var pages = ReportPageModels.Create(report);
        scenarios[0].Status = Status.Failed;
        Assert.That(pages.Summary.Layout.FailureCauses, Is.Zero);
    }

    private static void AssertNoSourceGraph(object? value, HashSet<object> seen)
    {
        if (value == null || value is string || value.GetType().IsValueType || !seen.Add(value)) return;
        Assert.That(value, Is.Not.InstanceOf<BDTestOutputModel>());
        Assert.That(value, Is.Not.InstanceOf<Scenario>());
        Assert.That(value, Is.Not.InstanceOf<Step>());
        Assert.That(value, Is.Not.InstanceOf<ExceptionWrapper>());
        if (value is IDictionary dictionary)
        {
            foreach (var item in dictionary.Values) AssertNoSourceGraph(item, seen);
        }
        else if (value is IEnumerable items)
        {
            foreach (var item in items) AssertNoSourceGraph(item, seen);
        }
        else
            foreach (var property in value.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0))
                AssertNoSourceGraph(property.GetValue(value), seen);
    }

    [Test]
    public async Task Repeated_reads_reuse_projections_and_replacement_and_deletion_invalidate()
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        var store = services.GetRequiredService<IMemoryCacheBdTestDataStore>();
        await repository.StoreData("compact", Report());
        var reads = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => repository.GetReportPages("compact", default)));
        Assert.That(reads.All(p => ReferenceEquals(p, reads[0])), Is.True);
        await store.StoreTestData("compact", Report(21));
        var replaced = Required(await repository.GetReportPages("compact", default));
        Assert.That(replaced, Is.Not.SameAs(reads[0]));
        Assert.That(replaced.Summary.Layout.ScenarioCount, Is.EqualTo(21));
        await repository.DeleteReport("compact");
        Assert.That(await repository.GetReportPages("compact", default), Is.Null);
        await repository.StoreData("compact", Report(7));
        Assert.That(Required(await repository.GetReportPages("compact", default)).Summary.Layout.ScenarioCount, Is.EqualTo(7));
    }

    [Test]
    public async Task Byte_store_reads_cache_projections_and_invalidate_after_byte_replacement()
    {
        using var services = Services();
        var store = services.GetRequiredService<IMemoryCacheBdTestDataStore>();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(BDTestJsonHelper.Serialize(Report())));
        await store.WriteAsync("compact", bytes, default);
        var repository = services.GetRequiredService<IDataRepository>();
        var pages = await repository.GetReportPages("compact", default);
        Assert.That(await repository.GetReportPages("compact", default), Is.SameAs(pages));
        using var replacement = new MemoryStream(Encoding.UTF8.GetBytes(BDTestJsonHelper.Serialize(Report(28))));
        await store.WriteAsync("compact", replacement, default);
        Assert.That(Required(await repository.GetReportPages("compact", default)).Summary.Layout.ScenarioCount, Is.EqualTo(28));
    }

    [Test]
    public async Task Oversized_projections_bypass_the_page_cache_without_losing_the_report()
    {
        using var services = Services(pageCapacity: 1);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("compact", Report());
        var first = Required(await repository.GetReportPages("compact", default));
        var second = Required(await repository.GetReportPages("compact", default));
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second.Summary.Layout.ScenarioCount, Is.EqualTo(first.Summary.Layout.ScenarioCount));
        Assert.That(await repository.GetData("compact", default), Is.Not.Null);
    }

    [Test]
    public async Task Page_capacity_evicts_projections_without_evicting_full_reports()
    {
        var size = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ReportPageModels.Create(Report()),
            new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<Status>() } }).Length;
        using var services = Services(pageCapacity: size + 100);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("compact", Report());
        var first = await repository.GetReportPages("compact", default);
        await repository.StoreData("another", Report(id: "another"));
        var second = await repository.GetReportPages("another", default);
        Assert.That(await repository.GetReportPages("another", default), Is.SameAs(second));
        Assert.That(await repository.GetReportPages("compact", default), Is.Not.SameAs(first));
        Assert.That(await repository.GetData("another", default), Is.Not.Null);
    }

    [Test]
    public async Task Full_report_capacity_eviction_also_hides_its_cached_projection()
    {
        var report = Report();
        report.TestTimer = new BDTest.Output.TestTimer(); // Match repository normalization before measuring.
        var size = Encoding.UTF8.GetByteCount(BDTestJsonHelper.Serialize(report));
        using var services = Services(reportCapacity: size + 100);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("compact", report);
        Assert.That(await repository.GetReportPages("compact", default), Is.Not.Null);
        await repository.StoreData("another", Report(id: "another"));
        Assert.That(await repository.GetReportPages("compact", default), Is.Null);
        Assert.That(await repository.GetReportPages("another", default), Is.Not.Null);
    }

    [Test]
    public async Task Expired_reports_do_not_survive_in_the_projection_cache()
    {
        using var services = Services(retention: TimeSpan.FromSeconds(2));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("compact", Report());
        Assert.That(await repository.GetReportPages("compact", default), Is.Not.Null);
        await Task.Delay(2100);
        Assert.That(await repository.GetReportPages("compact", default), Is.Null);
    }

    [Test]
    public async Task Cached_ordering_observes_request_culture_and_all_variants_are_invalidated()
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        var report = Report();
        report.NotRun = ReportJson.DeserializeObject<List<ScenarioDefinitionSummary>>("""
            [{ "ScenarioText": "Ångström", "StoryText": "A" }, { "ScenarioText": "Zebra", "StoryText": "A" }]
            """);
        await repository.StoreData("compact", report);
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            ReportPageModels? english = null;
            foreach (var culture in new[] { "en-US", "sv-SE" })
            {
                System.Globalization.CultureInfo.CurrentCulture = new(culture);
                var pages = Required(await repository.GetReportPages("compact", default));
                Assert.That(pages.Warnings.NotRun.Select(row => row.Scenario),
                    Is.EqualTo(report.NotRun.OrderBy(row => row.GetScenarioText()).Select(row => row.GetScenarioText())));
                Assert.That(await repository.GetReportPages("compact", default), Is.SameAs(pages));
                if (english == null) english = pages;
                else Assert.That(pages, Is.Not.SameAs(english));
            }
            await repository.DeleteReport("compact");
            System.Globalization.CultureInfo.CurrentCulture = new("en-US");
            Assert.That(await repository.GetReportPages("compact", default), Is.Null);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [Test]
    public async Task Historical_null_collections_missing_reports_and_cancellation_are_preserved()
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("old", new BDTestOutputModel { Id = "old", Scenarios = null, NotRun = null, TestTimer = null });
        var pages = Required(await repository.GetReportPages("old", default));
        Assert.That(pages.Summary.Layout.ScenarioCount, Is.Zero);
        Assert.That(pages.Timings.Median, Is.EqualTo(TimeSpan.Zero));
        Assert.That(pages.Warnings.NotRun, Is.Empty);
        var controller = new BDTestController(repository, Options.Create(new BDTestReportOptions()))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        foreach (var action in new Func<string, CancellationToken, Task<IActionResult>>[] { controller.Summary, controller.Timings, controller.TopDefects, controller.Warnings })
        {
            Assert.That(((ViewResult)await action("missing", default)).ViewName, Is.EqualTo("NotFoundSingle"));
            await Assert.CatchAsync<OperationCanceledException>(async () => await action("old", new CancellationToken(true)));
        }
    }

    [TestCase("summary")]
    [TestCase("timings")]
    [TestCase("defects")]
    [TestCase("warnings")]
    public void Ten_thousand_scenarios_allocate_less_on_repeated_page_preparation(string page)
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        var report = Report(10_000);
        repository.StoreData("compact", report).GetAwaiter().GetResult();
        var controller = new BDTestController(repository, Options.Create(new BDTestReportOptions()))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Func<string, CancellationToken, Task<IActionResult>> action = page switch
        {
            "summary" => controller.Summary, "timings" => controller.Timings,
            "defects" => controller.TopDefects, _ => controller.Warnings
        };
        void Cached() => GC.KeepAlive(action("compact", default).GetAwaiter().GetResult());
        void Prior()
        {
            // The previous views prepared layout data and report-wide aggregates on every request.
            var scenarios = Required(report.Scenarios).Where(s => s != null).ToList();
            GC.KeepAlive(Enum.GetValues<Status>().ToDictionary(status => status, status => scenarios.Count(s => s.Status == status)));
            GC.KeepAlive(scenarios.GetTotalStatus());
            GC.KeepAlive(BDTestUtil.GetTestTimer(scenarios.ToList(), report));
            GC.KeepAlive(FailureGroup.Build(scenarios, "location"));
            if (page is "summary" or "defects") GC.KeepAlive(FailureGroup.Build(scenarios, "location"));
            if (page == "summary")
            {
                GC.KeepAlive(scenarios.Where(s => s.Status is not (Status.Passed or Status.Failed)).OrderBy(s => s.Status.GetOrder()).ThenBy(s => s.GetScenarioText()).ToList());
                GC.KeepAlive(scenarios.OrderByDescending(s => s.TimeTaken).Take(5).ToList());
            }
            if (page == "timings") GC.KeepAlive(scenarios.Where(s => s.Status is not (Status.Skipped or Status.NotRun)).OrderBy(s => s.TimeTaken).ToList());
            if (page == "warnings") GC.KeepAlive(Required(report.NotRun).OrderBy(s => s.GetStoryText()).ThenBy(s => s.GetScenarioText()).ToList());
        }
        static long Measure(Action prepare)
        {
            prepare();
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10; i++) prepare();
            return (GC.GetAllocatedBytesForCurrentThread() - start) / 10;
        }
        var prior = Measure(Prior);
        var cached = Measure(Cached);
        TestContext.Out.WriteLine($"{page}: prior {prior:N0} bytes/view; cached {cached:N0} bytes/view (10,000 scenarios; preparation only, excluding HTML rendering/history)");
        Assert.That(cached, Is.LessThan(prior / 10));
    }
}
