using System.Text;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportCacheCapacityTests
{
    [Test]
    public async Task Uploads_and_ordinary_reads_evict_reports_to_stay_within_capacity()
    {
        var reports = Enumerable.Range(0, 20).Select(i => Report(i.ToString())).ToArray();
        var capacity = Size(reports[0]) * 3;
        var store = new PersistentStore();
        using var services = Services(capacity, store);
        var repository = services.GetRequiredService<IDataRepository>();

        foreach (var report in reports)
        {
            await repository.StoreData(report, report.Id);
            await AssertWithinCapacity(repository, reports, capacity, store);
        }

        foreach (var report in reports)
        {
            Assert.That(await repository.GetData(report.Id, default), Is.Not.Null);
            await AssertWithinCapacity(repository, reports, capacity, store);
        }
    }

    [Test]
    public async Task Oversized_reports_bypass_cache_when_persistent_storage_is_configured()
    {
        var report = Report("large", 1000);
        var store = new PersistentStore();
        using var services = Services(500, store);
        var repository = services.GetRequiredService<IDataRepository>();

        await repository.StoreData(report, report.Id);
        Assert.That(store.Reports, Contains.Key(report.Id));
        await repository.GetData(report.Id, default);
        await repository.GetData(report.Id, default);
        Assert.That(store.Reads, Is.EqualTo(2));
    }

    [Test]
    public async Task Cache_only_oversized_reports_are_rejected_without_summary_or_eviction()
    {
        using var services = Services(500);
        var repository = services.GetRequiredService<IDataRepository>();
        var small = Report("small");
        await repository.StoreData(small, small.Id);

        await Assert.CatchAsync<InvalidOperationException>(async () =>
            await repository.StoreData(Report("large", 1000), "large"));
        Assert.That(await repository.GetData("small", default), Is.SameAs(small));
        Assert.That((await repository.GetAllTestRunRecords()).Select(x => x.RecordId), Is.EqualTo(new[] { "small" }));
    }

    [Test]
    public async Task Host_cache_entries_need_no_sizes_and_do_not_share_report_keys()
    {
        using var services = Services(500);
        var hostCache = services.GetRequiredService<IMemoryCache>();
        hostCache.Set("same", "host value");
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Report("same"), "same");
        Assert.That(hostCache.Get<string>("same"), Is.EqualTo("host value"));
        await repository.DeleteReport("same");
        Assert.That(hostCache.Get<string>("same"), Is.EqualTo("host value"));
    }

    [Test]
    public async Task Concurrent_cache_only_uploads_remain_bounded_and_admit_new_reports()
    {
        var reports = Enumerable.Range(0, 50).Select(i => Report(i.ToString())).ToArray();
        var capacity = Size(reports[0]) * 3;
        using var services = Services(capacity);
        var repository = services.GetRequiredService<IDataRepository>();

        await Parallel.ForEachAsync(reports, async (report, _) =>
            await repository.StoreData(report, report.Id));

        await AssertWithinCapacity(repository, reports, capacity);
        var newest = Report("newest");
        await repository.StoreData(newest, newest.Id);
        Assert.That(await repository.GetDataForBulkRead(newest.Id, default), Is.SameAs(newest));
    }

    [Test]
    public async Task Concurrent_refreshes_never_temporarily_hide_a_cache_only_report()
    {
        var report = Report("same");
        using var services = Services(Size(report));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(report, report.Id);

        await Parallel.ForEachAsync(Enumerable.Range(0, 2000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (_, _) => Assert.That(await repository.GetData(report.Id, default), Is.SameAs(report)));
    }

    [Test]
    public async Task Reports_expire_after_configured_retention_in_cache_only_mode()
    {
        using var services = Services(1000);
        services.GetRequiredService<BDTestReportServerOptions>().ReportCacheRetention = TimeSpan.FromMilliseconds(10);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Report("expired"), "expired");

        await Task.Delay(40);

        Assert.That(await repository.GetDataForBulkRead("expired", default), Is.Null);
    }

    [Test]
    public async Task Replacing_and_deleting_reports_releases_capacity()
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        var replacement = Report("same");
        var other = Report("other", 500);
        using var services = Services(Size(replacement) + Size(other));
        var options = services.GetRequiredService<BDTestReportServerOptions>();
        using var cache = new MemoryCacheBdTestDataStore(hostCache, options);
        await cache.StoreTestData("same", Report("same", 500));
        await cache.StoreTestData("same", replacement);
        await cache.StoreTestData("other", other);
        Assert.That(await cache.GetTestData("same", default), Is.SameAs(replacement));
        await cache.DeleteTestData("other");
        await cache.StoreTestData("third", Report("third", 499));
        Assert.That(await cache.GetTestData("same", default), Is.SameAs(replacement));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Nonpositive_capacity_and_retention_are_rejected(long value)
    {
        using var services = Services(1000);
        var options = services.GetRequiredService<BDTestReportServerOptions>();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ReportCacheSizeLimitBytes = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ReportCacheRetention = TimeSpan.FromTicks(value));
    }

    private static async Task AssertWithinCapacity(IDataRepository repository, BDTestOutputModel[] reports,
        long capacity, PersistentStore persistentStore = null)
    {
        // Disable fallback so bulk reads reveal only the actual retained reports.
        if (persistentStore != null) persistentStore.HideReports = true;
        try
        {
            var retained = new List<BDTestOutputModel>();
            foreach (var report in reports)
            {
                var cached = await repository.GetDataForBulkRead(report.Id, default);
                if (cached != null) retained.Add(cached);
            }
            Assert.That(retained.Sum(Size), Is.LessThanOrEqualTo(capacity));
            Assert.That(retained, Is.Not.Empty);
        }
        finally
        {
            if (persistentStore != null) persistentStore.HideReports = false;
        }
    }

    private static ServiceProvider Services(long capacity, IBDTestDataStore store = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BDTestReportServerOptions>();
        options.ReportCacheSizeLimitBytes = capacity;
        options.DataStore = store;
        return provider;
    }

    private static BDTestOutputModel Report(string id, int length = 1) => new()
    {
        Id = id,
        Tag = new string('x', length),
        TestTimer = new BDTest.Output.TestTimer { TestsStartedAt = default, TestsFinishedAt = default }
    };

    private static long Size(BDTestOutputModel report) => Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(report));

    private sealed class PersistentStore : IBDTestDataStore
    {
        public Dictionary<string, BDTestOutputModel> Reports { get; } = new();
        public bool HideReports { get; set; }
        public int Reads { get; private set; }
        public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(HideReports ? null : Reports.GetValueOrDefault(id));
        }
        public Task StoreTestData(string id, BDTestOutputModel data) { Reports[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Reports.Remove(id); return Task.CompletedTask; }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreTestRunRecord(TestRunSummary summary) => Task.CompletedTask;
        public Task DeleteTestRunRecord(string id) => Task.CompletedTask;
    }
}
