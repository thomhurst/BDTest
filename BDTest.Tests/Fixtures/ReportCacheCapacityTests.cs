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
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

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
            await repository.StoreData(Required(report.Id), report);
            await AssertWithinCapacity(repository, reports, capacity, store);
        }

        foreach (var report in reports)
        {
            await Assert.That(await repository.GetData(Required(report.Id), default)).IsNotNull();
            await AssertWithinCapacity(repository, reports, capacity, store);
        }
    }

    [Test]
    public async Task Duplicated_values_fit_the_plain_JSON_upload_size()
    {
        var scenario = new BDTest.Contracts.ScenarioResult("scenario", "shared", "story", "scenario", Status.Passed,
            output: new string('x', 10000));
        var report = new BDTest.Contracts.BDTestReport(1, "shared", "test", Enumerable.Repeat(scenario, 100).ToArray());
        // Reproduce the object graph restored by the MVC binder from a supported upload.
        report = BDTest.Contracts.BDTestReportJson.Deserialize(BDTestJsonHelper.Serialize(report));
        await Assert.That(Required(report.Scenarios)[1]).IsNotSameReferenceAs(report.Scenarios[0]);
        using var services = Services(Size(report));
        var repository = services.GetRequiredService<IDataRepository>();

        await repository.StoreData(Required(report.Id), report);

        await Assert.That(await repository.GetDataForBulkRead(report.Id, default)).IsSameReferenceAs(report);
    }

    [Test]
    public async Task Unicode_reports_use_the_exact_UTF8_byte_budget()
    {
        var report = new BDTest.Contracts.BDTestReport(1, "unicode", "test", tag: new string('x', 1023) + "😀漢字é");
        using var exactServices = Services(Size(report));
        var repository = exactServices.GetRequiredService<IDataRepository>();
        await repository.StoreData(Required(report.Id), report);
        await Assert.That(await repository.GetDataForBulkRead(report.Id, default)).IsSameReferenceAs(report);

        using var smallerServices = Services(Size(report) - 1);
        var smallerRepository = smallerServices.GetRequiredService<IDataRepository>();
        await Assert.That(async () =>
            await smallerRepository.StoreData(report.Id, report)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(Formatting.Indented, NullValueHandling.Include)]
    [Arguments(Formatting.None, NullValueHandling.Ignore)]
    [NotInParallel]
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

            await repository.StoreData(Required(report.Id), report);

            await Assert.That(await repository.GetDataForBulkRead(report.Id, default)).IsSameReferenceAs(report);
            await Assert.That(async () =>
                await smallerRepository.StoreData(report.Id, report)).Throws<InvalidOperationException>();
            await Assert.That(defaultSettingsCalls).IsZero().Because("Cache accounting must not consult host JSON defaults.");
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

        await repository.StoreData(Required(report.Id), report);
        await Assert.That(store.Reports).ContainsKey(Required(report.Id));
        await repository.GetData(report.Id, default);
        await repository.GetData(report.Id, default);
        await Assert.That(store.Reads).IsEqualTo(2);
    }

    [Test]
    public async Task Cache_only_oversized_reports_are_rejected_without_summary_or_eviction()
    {
        using var services = Services(500);
        var repository = services.GetRequiredService<IDataRepository>();
        var small = Report("small");
        await repository.StoreData(Required(small.Id), small);

        await Assert.That(async () =>
            await repository.StoreData("large", Report("large", 1000))).Throws<InvalidOperationException>();
        await Assert.That(await repository.GetData("small", default)).IsSameReferenceAs(small);
        await Assert.That((await repository.GetAllTestRunRecords()).Select(x => x.RecordId)).IsEquivalentTo((new[] { "small" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Host_cache_entries_need_no_sizes_and_do_not_share_report_keys()
    {
        using var services = Services(500);
        var hostCache = services.GetRequiredService<IMemoryCache>();
        hostCache.Set("same", "host value");
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("same", Report("same"));
        await Assert.That(hostCache.Get<string>("same")).IsEqualTo("host value");
        await repository.DeleteReport("same");
        await Assert.That(hostCache.Get<string>("same")).IsEqualTo("host value");
    }

    [Test]
    public async Task Concurrent_cache_only_uploads_remain_bounded_and_admit_new_reports()
    {
        var reports = Enumerable.Range(0, 50).Select(i => Report(i.ToString())).ToArray();
        var capacity = Size(reports[0]) * 3;
        using var services = Services(capacity);
        var repository = services.GetRequiredService<IDataRepository>();

        await Parallel.ForEachAsync(reports, async (report, _) =>
            await repository.StoreData(Required(report.Id), report));

        await AssertWithinCapacity(repository, reports, capacity);
        var newest = Report("newest");
        await repository.StoreData(Required(newest.Id), newest);
        await Assert.That(await repository.GetDataForBulkRead(newest.Id, default)).IsSameReferenceAs(newest);
    }

    [Test]
    public async Task Concurrent_refreshes_never_temporarily_hide_a_cache_only_report()
    {
        var report = Report("same");
        using var services = Services(Size(report));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Required(report.Id), report);

        await Parallel.ForEachAsync(Enumerable.Range(0, 2000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (_, _) => await Assert.That(await repository.GetData(report.Id, default)).IsSameReferenceAs(report));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cached_reads_reuse_the_admitted_snapshot_and_ignore_source_mutation(bool persistent)
    {
        var source = new BDTestOutputModel { Id = "cached", Tag = "original" };
        var report = source.ToReport();
        var store = persistent ? new PersistentStore() : null;
        using var services = Services(Size(report), store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Required(report.Id), report);

        // Mutation after publication cannot alter the immutable report or its admitted size.
        source.Tag = new string('x', 2000);
        for (var read = 0; read < 3; read++)
        {
            await Assert.That(await repository.GetData(report.Id, default)).IsSameReferenceAs(report);
        }
        await Assert.That(await repository.GetDataForBulkRead(report.Id, default)).IsSameReferenceAs(report);
        await Assert.That(report.Tag).IsEqualTo("original");
        if (store != null) await Assert.That(store.Reads).IsZero();
    }

    [Test]
    // Preserve the wall-clock probe's budget independently of unrelated parallel work.
    [NotInParallel]
    public async Task Ordinary_cached_reads_extend_retention_but_bulk_reads_do_not()
    {
        using var services = Services(10000, retention: TimeSpan.FromSeconds(2));
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("ordinary", Report("ordinary"));
        await repository.StoreData("bulk", Report("bulk"));

        await Task.Delay(1200);
        await Assert.That(await repository.GetData("ordinary", default)).IsNotNull();
        await Assert.That(await repository.GetDataForBulkRead("bulk", default)).IsNotNull();

        await Task.Delay(1200);

        await Assert.That(await repository.GetDataForBulkRead("ordinary", default)).IsNotNull();
        await Assert.That(await repository.GetDataForBulkRead("bulk", default)).IsNull();
    }

    [Test]
    public async Task Cached_reads_of_10000_scenarios_allocate_less_than_repeated_size_measurement()
    {
        var report = new BDTest.Contracts.BDTestReport(1, "large-run", "test",
            Enumerable.Range(0, 10000).Select(index => new BDTest.Contracts.ScenarioResult(
                index.ToString(), "large-run", "story", "scenario", Status.Passed)).ToArray());
        using var services = Services(Size(report) * 2);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Required(report.Id), report);
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var baselineCache = new MemoryCacheBdTestDataStore(hostCache,
            services.GetRequiredService<IOptions<BDTestReportOptions>>());

        // StoreTestData reproduces the full size-measurement/replacement work of the old read path.
        Action baseline = () => baselineCache.StoreTestData(report.Id, report).GetAwaiter().GetResult();
        Action read = () => repository.GetData(report.Id, default).GetAwaiter().GetResult();
        var small = Report("small-run");
        await repository.StoreData(small.Id, small);
        Action smallRead = () => repository.GetData(small.Id, default).GetAwaiter().GetResult();
        baseline();
        read();
        smallRead();
        var baselineBytes = AllocatedBytesPerCall(baseline);
        var cachedReadBytes = AllocatedBytesPerCall(read);
        Console.Out.WriteLine($"10,000 scenarios: repeated size measurement {baselineBytes:N0} bytes/read; cached read {cachedReadBytes:N0} bytes/read.");

        // Generated serialization of immutable contracts now allocates little even for large graphs.
        // Check the cache invariant directly instead of depending on the old mutable serializer's ratio.
        await Assert.That(cachedReadBytes).IsEqualTo(AllocatedBytesPerCall(smallRead))
            .Because("Cached read allocation must be independent of the number of scenarios.");
        await Assert.That(cachedReadBytes).IsLessThan(baselineBytes)
            .Because("A cache hit must avoid the serialization and replacement work.");
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

        await Assert.That(await repository.GetDataForBulkRead("expired", default)).IsNull();
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
        await Assert.That(await cache.GetTestData("same", default)).IsSameReferenceAs(replacement);
        await cache.DeleteTestData("other");
        await cache.StoreTestData("third", Report("third", 499));
        await Assert.That(await cache.GetTestData("same", default)).IsSameReferenceAs(replacement);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Nonpositive_capacity_and_retention_are_rejected(long value)
    {
        var services = new ServiceCollection();
        services.AddBDTestReports(options =>
        {
            options.ReportCacheSizeLimitBytes = value;
            options.ReportCacheRetention = TimeSpan.FromTicks(value);
        });
        using var provider = services.BuildServiceProvider();
        var exception = await Assert.That(() =>
            _ = provider.GetRequiredService<IOptions<BDTestReportOptions>>().Value).ThrowsExactly<OptionsValidationException>();
        await Assert.That(Required(exception).Failures).Count().IsEqualTo(2);
    }

    private static async Task AssertWithinCapacity(IDataRepository repository, BDTest.Contracts.BDTestReport[] reports,
        long capacity, PersistentStore? persistentStore = null)
    {
        // Disable fallback so bulk reads reveal only the actual retained reports.
        if (persistentStore != null) persistentStore.HideReports = true;
        try
        {
            var retained = new List<BDTest.Contracts.BDTestReport>();
            foreach (var report in reports)
            {
                var cached = await repository.GetDataForBulkRead(Required(report.Id), default);
                if (cached != null) retained.Add(cached);
            }
            await Assert.That(retained.Sum(Size)).IsLessThanOrEqualTo(capacity);
            await Assert.That(retained).IsNotEmpty();
        }
        finally
        {
            if (persistentStore != null) persistentStore.HideReports = false;
        }
    }

    private static ServiceProvider Services(long capacity, IBDTestDataStore? store = null, TimeSpan? retention = null)
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

    private static BDTest.Contracts.BDTestReport Report(string id, int length = 1) => new(
        1, id, "test", tag: new string('x', length), testTimer: new BDTest.Contracts.ReportTimer(default, default));

    private static long Size(BDTest.Contracts.BDTestReport report) => Encoding.UTF8.GetByteCount(BDTestJsonHelper.Serialize(report));

    private sealed class PersistentStore : IBDTestDataStore
    {
        public Dictionary<string, BDTest.Contracts.BDTestReport?> Reports { get; } = new();
        public bool HideReports { get; set; }
        public int Reads { get; private set; }
        public Task<BDTest.Contracts.BDTestReport?> GetTestData(string id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(HideReports ? null : Reports.GetValueOrDefault(id));
        }
        public Task StoreTestData(string id, BDTest.Contracts.BDTestReport? data) { Reports[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Reports.Remove(id); return Task.CompletedTask; }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreTestRunRecord(TestRunSummary summary) => Task.CompletedTask;
        public Task DeleteTestRunRecord(string id) => Task.CompletedTask;
    }
}
