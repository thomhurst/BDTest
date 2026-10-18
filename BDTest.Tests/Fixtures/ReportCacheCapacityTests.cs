using System.Text;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Test;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using Microsoft.Extensions.Options;

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
            await repository.StoreData(report.Id, report);
            await AssertWithinCapacity(repository, reports, capacity, store);
        }

        foreach (var report in reports)
        {
            Assert.That(await repository.GetData(report.Id, default), Is.Not.Null);
            await AssertWithinCapacity(repository, reports, capacity, store);
        }
    }

    [Test]
    public async Task Duplicated_values_fit_the_plain_JSON_upload_size()
    {
        var report = Report("shared");
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Output = new string('x', 10000),
            Steps = Array.Empty<Step>()
        }));
        report.Scenarios = Enumerable.Repeat(scenario, 100).ToList();
        // Reproduce the object graph restored by the MVC binder from a supported upload.
        report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.Serialize(report));
        Assert.That(report.Scenarios[1], Is.Not.SameAs(report.Scenarios[0]));
        using var services = Services(Size(report));
        var repository = services.GetRequiredService<IDataRepository>();

        await repository.StoreData(report.Id, report);

        Assert.That(await repository.GetDataForBulkRead(report.Id, default), Is.SameAs(report));
    }

    [Test]
    public async Task Unicode_reports_use_the_exact_UTF8_byte_budget()
    {
        var report = Report("unicode");
        report.Tag = new string('x', 1023) + "😀漢字é";
        using var exactServices = Services(Size(report));
        var repository = exactServices.GetRequiredService<IDataRepository>();
        await repository.StoreData(report.Id, report);
        Assert.That(await repository.GetDataForBulkRead(report.Id, default), Is.SameAs(report));

        using var smallerServices = Services(Size(report) - 1);
        var smallerRepository = smallerServices.GetRequiredService<IDataRepository>();
        await Assert.CatchAsync<InvalidOperationException>(async () =>
            await smallerRepository.StoreData(report.Id, report));
    }

    [TestCase(Formatting.Indented, NullValueHandling.Include)]
    [TestCase(Formatting.None, NullValueHandling.Ignore)]
    [NonParallelizable]
    public async Task Host_JSON_defaults_do_not_change_report_capacity(Formatting formatting, NullValueHandling nullValueHandling)
    {
        var report = Report("host-defaults");
        var size = Size(report);
        using var exactServices = Services(size);
        using var smallerServices = Services(size - 1);
        var repository = exactServices.GetRequiredService<IDataRepository>();
        var smallerRepository = smallerServices.GetRequiredService<IDataRepository>();
        var originalDefaults = JsonConvert.DefaultSettings;
        var defaultSettingsCalls = 0;
        try
        {
            JsonConvert.DefaultSettings = () =>
            {
                defaultSettingsCalls++;
                return new JsonSerializerSettings
                {
                    Formatting = formatting,
                    NullValueHandling = nullValueHandling
                };
            };

            await repository.StoreData(report.Id, report);

            Assert.That(await repository.GetDataForBulkRead(report.Id, default), Is.SameAs(report));
            await Assert.CatchAsync<InvalidOperationException>(async () =>
                await smallerRepository.StoreData(report.Id, report));
            Assert.That(defaultSettingsCalls, Is.Zero, "Cache accounting must not consult host JSON defaults.");
        }
        finally
        {
            JsonConvert.DefaultSettings = originalDefaults;
        }
    }

    [Test]
    public async Task Oversized_reports_bypass_cache_when_persistent_storage_is_configured()
    {
        var report = Report("large", 1000);
        var store = new PersistentStore();
        using var services = Services(500, store);
        var repository = services.GetRequiredService<IDataRepository>();

        await repository.StoreData(report.Id, report);
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
        await repository.StoreData(small.Id, small);

        await Assert.CatchAsync<InvalidOperationException>(async () =>
            await repository.StoreData("large", Report("large", 1000)));
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
        await repository.StoreData("same", Report("same"));
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
            await repository.StoreData(report.Id, report));

        await AssertWithinCapacity(repository, reports, capacity);
        var newest = Report("newest");
        await repository.StoreData(newest.Id, newest);
        Assert.That(await repository.GetDataForBulkRead(newest.Id, default), Is.SameAs(newest));
    }

    [Test]
    public async Task Concurrent_refreshes_never_temporarily_hide_a_cache_only_report()
    {
        var report = Report("same");
        using var services = Services(Size(report));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(report.Id, report);

        await Parallel.ForEachAsync(Enumerable.Range(0, 2000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (_, _) => Assert.That(await repository.GetData(report.Id, default), Is.SameAs(report)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Cached_reads_reuse_the_size_measured_at_admission(bool persistent)
    {
        var report = Report("cached");
        var store = persistent ? new PersistentStore() : null;
        using var services = Services(Size(report), store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(report.Id, report);

        // A deliberate mutation detects size measurement without relying on serializer internals.
        // Callers must otherwise treat cached reports as immutable.
        report.Tag = new string('x', 2000);
        for (var read = 0; read < 3; read++)
        {
            Assert.That(await repository.GetData(report.Id, default), Is.SameAs(report));
        }
        Assert.That(await repository.GetDataForBulkRead(report.Id, default), Is.SameAs(report));
        if (store != null) Assert.That(store.Reads, Is.Zero);
    }

    [Test]
    public async Task Ordinary_cached_reads_extend_retention_but_bulk_reads_do_not()
    {
        using var services = Services(10000, retention: TimeSpan.FromSeconds(2));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("ordinary", Report("ordinary"));
        await repository.StoreData("bulk", Report("bulk"));

        await Task.Delay(1200);
        Assert.That(await repository.GetData("ordinary", default), Is.Not.Null);
        Assert.That(await repository.GetDataForBulkRead("bulk", default), Is.Not.Null);

        await Task.Delay(1200);

        Assert.That(await repository.GetDataForBulkRead("ordinary", default), Is.Not.Null);
        Assert.That(await repository.GetDataForBulkRead("bulk", default), Is.Null);
    }

    [Test]
    public async Task Cached_reads_of_10000_scenarios_allocate_less_than_repeated_size_measurement()
    {
        var report = Report("large-run");
        report.Scenarios = Enumerable.Range(0, 10000).Select(index => new Scenario
        {
            Guid = index.ToString(),
            Steps = new List<Step>()
        }).ToList();
        using var services = Services(Size(report) * 2);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(report.Id, report);
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var baselineCache = new MemoryCacheBdTestDataStore(hostCache,
            services.GetRequiredService<IOptions<BDTestReportOptions>>());

        // StoreTestData reproduces the full size-measurement/replacement work of the old read path.
        Action baseline = () => baselineCache.StoreTestData(report.Id, report).GetAwaiter().GetResult();
        Action read = () => repository.GetData(report.Id, default).GetAwaiter().GetResult();
        baseline();
        read();
        var baselineBytes = AllocatedBytesPerCall(baseline);
        var cachedReadBytes = AllocatedBytesPerCall(read);
        TestContext.Out.WriteLine($"10,000 scenarios: repeated size measurement {baselineBytes:N0} bytes/read; cached read {cachedReadBytes:N0} bytes/read.");

        Assert.That(cachedReadBytes, Is.LessThan(baselineBytes / 10),
            "A cache hit must avoid report-wide serialization allocations.");
    }

    private static long AllocatedBytesPerCall(Action action)
    {
        const int iterations = 5;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < iterations; iteration++) action();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    }

    [Test]
    public async Task Reports_expire_after_configured_retention_in_cache_only_mode()
    {
        using var services = Services(1000, retention: TimeSpan.FromMilliseconds(10));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("expired", Report("expired"));

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
        var options = services.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        using var cache = new MemoryCacheBdTestDataStore(hostCache, Options.Create(options));
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
        var services = new ServiceCollection();
        services.AddBDTestReports(options =>
        {
            options.ReportCacheSizeLimitBytes = value;
            options.ReportCacheRetention = TimeSpan.FromTicks(value);
        });
        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<BDTestReportOptions>>().Value);
        Assert.That(exception.Failures, Has.Count.EqualTo(2));
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

    private static ServiceProvider Services(long capacity, IBDTestDataStore store = null, TimeSpan? retention = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports();
        services.Configure<BDTestReportOptions>(options =>
        {
            options.ReportCacheSizeLimitBytes = capacity;
            options.ReportCacheRetention = retention ?? TimeSpan.FromHours(8);
        });
        if (store != null) services.AddSingleton<IBDTestDataStore>(store);
        return services.BuildServiceProvider();
    }

    private static BDTestOutputModel Report(string id, int length = 1) => new()
    {
        Id = id,
        Tag = new string('x', length),
        TestTimer = new BDTest.Output.TestTimer { TestsStartedAt = default, TestsFinishedAt = default }
    };

    private static long Size(BDTestOutputModel report) => Encoding.UTF8.GetByteCount(BDTestJsonHelper.Serialize(report));

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
