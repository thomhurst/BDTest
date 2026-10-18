using System.Collections.Concurrent;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class DataRepositoryHistoryTests
{
    [Test]
    public async Task Upload_before_first_read_preserves_persisted_history_and_loads_only_once()
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();

        await repository.StoreData(Report("new", 2), "new");

        for (var read = 0; read < 2; read++)
        {
            var records = await repository.GetAllTestRunRecords();
            Assert.That(records.Select(record => record.RecordId), Is.EqualTo(new[] { "new", "old" }));
        }
        Assert.That(store.Loads, Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_first_reads_share_one_load_and_include_uploads_during_loading()
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("old", 1));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Read = async () =>
        {
            var snapshot = store.Records.ToArray();
            started.TrySetResult();
            await release.Task;
            return snapshot;
        };
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        var reads = Enumerable.Range(0, 20).Select(_ => repository.GetAllTestRunRecords()).ToArray();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(store.Loads, Is.EqualTo(1));
            Assert.That(reads.All(read => !read.IsCompleted), Is.True);
            await repository.StoreData(Report("new", 2), "new");
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(reads);
        }

        foreach (var read in reads)
        {
            Assert.That(read.Result.Select(record => record.RecordId), Is.EqualTo(new[] { "new", "old" }));
        }
        Assert.That(store.Loads, Is.EqualTo(1));
    }

    [Test]
    public async Task History_is_deduplicated_and_sorted_with_cached_records_preserved()
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("middle", 2));
        store.Records.Add(Summary("old", 1));
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Report("new", 3), "new");
        store.Records.Add(Summary("new", 0));

        var records = (await repository.GetAllTestRunRecords()).ToArray();

        Assert.That(records.Select(record => record.RecordId), Is.EqualTo(new[] { "new", "middle", "old" }));
        Assert.That(records[0].StartedAtDateTime, Is.EqualTo(Report("new", 3).TestTimer.TestsStartedAt));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Empty_or_null_history_is_loaded_only_once(bool returnsNull)
    {
        var store = new PersistentStore();
        if (returnsNull) store.Read = () => Task.FromResult<IEnumerable<TestRunSummary>>(null);
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();

        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
        await repository.StoreData(Report("new", 1), "new");
        Assert.That((await repository.GetAllTestRunRecords()).Single().RecordId, Is.EqualTo("new"));
        Assert.That(store.Loads, Is.EqualTo(1));
    }

    [Test]
    public async Task Failed_history_load_can_be_retried_without_losing_cached_uploads()
    {
        var failure = new InvalidOperationException("storage unavailable");
        var store = new PersistentStore { Read = () => throw failure };
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Report("new", 2), "new");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAllTestRunRecords());
        Assert.That(thrown, Is.SameAs(failure));
        store.Read = null;

        Assert.That((await repository.GetAllTestRunRecords()).Select(record => record.RecordId),
            Is.EqualTo(new[] { "new", "old" }));
        await repository.GetAllTestRunRecords();
        Assert.That(store.Loads, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Deletion_removes_cached_and_persisted_history_before_or_after_loading(bool loadFirst)
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData(Report("new", 2), "new");
        if (loadFirst) await repository.GetAllTestRunRecords();

        await repository.DeleteReport("old");
        await repository.DeleteReport("new");

        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
        Assert.That(await repository.GetData("new", default), Is.Null);
        Assert.That(store.Records, Is.Empty);
        Assert.That(store.Reports, Is.Empty);
        Assert.That(store.Loads, Is.EqualTo(1));
    }

    [Test]
    public async Task Deletion_during_loading_does_not_resurrect_a_persisted_record()
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("old", 1));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Read = async () =>
        {
            var snapshot = store.Records.ToArray();
            started.TrySetResult();
            await release.Task;
            return snapshot;
        };
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        var read = repository.GetAllTestRunRecords();
        Task deletion = Task.CompletedTask;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            deletion = repository.DeleteReport("old");
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(read, deletion);
        }

        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
        Assert.That(store.Records, Is.Empty);
        Assert.That(store.Loads, Is.EqualTo(1));
    }

    [Test]
    public async Task Memory_only_history_still_tracks_uploads_and_deletions()
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        Assert.That(await repository.GetAllTestRunRecords(), Is.Empty);
        await repository.StoreData(Report("old", 1), "old");
        await repository.StoreData(Report("new", 2), "new");
        Assert.That((await repository.GetAllTestRunRecords()).Select(record => record.RecordId),
            Is.EqualTo(new[] { "new", "old" }));

        await repository.DeleteReport("new");

        Assert.That((await repository.GetAllTestRunRecords()).Single().RecordId, Is.EqualTo("old"));
        Assert.That(await repository.GetData("new", default), Is.Null);
    }

    private static ServiceProvider Services(IBDTestDataStore store = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BDTestReportServerOptions>().DataStore = store;
        return provider;
    }

    private static BDTestOutputModel Report(string id, int day) => new()
    {
        Id = id,
        TestTimer = new TestTimer
        {
            TestsStartedAt = new DateTime(2026, 1, 1).AddDays(day),
            TestsFinishedAt = new DateTime(2026, 1, 1).AddDays(day).AddMinutes(1)
        }
    };

    private static TestRunSummary Summary(string id, int day) =>
        ReportJson.DeserializeObject<TestRunSummary>(ReportJson.SerializeObject(new
        {
            RecordId = id,
            StartedAtDateTime = Report(id, day).TestTimer.TestsStartedAt
        }));

    private sealed class PersistentStore : IBDTestDataStore
    {
        public List<TestRunSummary> Records { get; } = [];
        public ConcurrentDictionary<string, BDTestOutputModel> Reports { get; } = new();
        public Func<Task<IEnumerable<TestRunSummary>>> Read { get; set; }
        public int Loads;

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            Interlocked.Increment(ref Loads);
            return Read?.Invoke() ?? Task.FromResult<IEnumerable<TestRunSummary>>(Records.ToArray());
        }

        public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Reports.GetValueOrDefault(id));

        public Task StoreTestData(string id, BDTestOutputModel data) { Reports[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Reports.TryRemove(id, out _); return Task.CompletedTask; }
        public Task StoreTestRunRecord(TestRunSummary summary) { Records.Add(summary); return Task.CompletedTask; }
        public Task DeleteTestRunRecord(string id) { Records.RemoveAll(record => record.RecordId == id); return Task.CompletedTask; }
    }
}
