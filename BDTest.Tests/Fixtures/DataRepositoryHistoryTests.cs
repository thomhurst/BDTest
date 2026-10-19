using System.Collections.Concurrent;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Helpers;
using BDTest.Razor.Reports.Storage;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

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

        await repository.StoreData("new", (Report("new", 2)).ToReport());

        for (var read = 0; read < 2; read++)
        {
            var records = await repository.GetAllTestRunRecords();
            await Assert.That(records.Select(record => record.RecordId)).IsEquivalentTo((new[] { "new", "old" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        await Assert.That(store.Loads).IsEqualTo(1);
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
            await Assert.That(store.Loads).IsEqualTo(1);
            await Assert.That(reads.All(read => !read.IsCompleted)).IsTrue();
            await repository.StoreData("new", (Report("new", 2)).ToReport());
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(reads);
        }

        foreach (var read in reads)
        {
            await Assert.That(read.Result.Select(record => record.RecordId)).IsEquivalentTo((new[] { "new", "old" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        await Assert.That(store.Loads).IsEqualTo(1);
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
        await repository.StoreData("new", (Report("new", 3)).ToReport());
        store.Records.Add(Summary("new", 0));

        var records = (await repository.GetAllTestRunRecords()).ToArray();

        await Assert.That(records.Select(record => record.RecordId)).IsEquivalentTo((new[] { "new", "middle", "old" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(records[0].StartedAtDateTime).IsEqualTo(Required(Report("new", 3).TestTimer).TestsStartedAt);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Empty_or_null_history_is_loaded_only_once(bool returnsNull)
    {
        var store = new PersistentStore();
        if (returnsNull) store.Read = () => Task.FromResult<IEnumerable<TestRunSummary>>(null!);
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();

        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await repository.StoreData("new", (Report("new", 1)).ToReport());
        await Assert.That((await repository.GetAllTestRunRecords()).Single().RecordId).IsEqualTo("new");
        await Assert.That(store.Loads).IsEqualTo(1);
    }

    [Test]
    public async Task Failed_history_load_can_be_retried_without_losing_cached_uploads()
    {
        var failure = new InvalidOperationException("storage unavailable");
        var store = new PersistentStore { Read = () => throw failure };
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("new", (Report("new", 2)).ToReport());

        var thrown = await Assert.That(async () => { await repository.GetAllTestRunRecords(); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(thrown).IsSameReferenceAs(failure);
        store.Read = null;

        await Assert.That((await repository.GetAllTestRunRecords()).Select(record => record.RecordId)).IsEquivalentTo((new[] { "new", "old" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await repository.GetAllTestRunRecords();
        await Assert.That(store.Loads).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deletion_removes_cached_and_persisted_history_before_or_after_loading(bool loadFirst)
    {
        var store = new PersistentStore();
        store.Records.Add(Summary("old", 1));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await repository.StoreData("new", (Report("new", 2)).ToReport());
        if (loadFirst) await repository.GetAllTestRunRecords();

        await repository.DeleteReport("old");
        await repository.DeleteReport("new");

        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(await repository.GetData("new", default)).IsNull();
        await Assert.That(store.Records).IsEmpty();
        await Assert.That(store.Reports).IsEmpty();
        await Assert.That(store.Loads).IsEqualTo(1);
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

        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(store.Records).IsEmpty();
        await Assert.That(store.Loads).IsEqualTo(1);
    }

    [Test]
    public async Task Memory_only_history_still_tracks_uploads_and_deletions()
    {
        using var services = Services();
        var repository = services.GetRequiredService<IDataRepository>();
        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await repository.StoreData("old", (Report("old", 1)).ToReport());
        await repository.StoreData("new", (Report("new", 2)).ToReport());
        await Assert.That((await repository.GetAllTestRunRecords()).Select(record => record.RecordId)).IsEquivalentTo((new[] { "new", "old" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await repository.DeleteReport("new");

        await Assert.That((await repository.GetAllTestRunRecords()).Single().RecordId).IsEqualTo("old");
        await Assert.That(await repository.GetData("new", default)).IsNull();
    }

    [Test]
    public async Task History_load_follows_all_continuations_once()
    {
        var store = new PersistentStore();
        for (var i = 0; i < 1201; i++) store.Records.Add(Summary(i.ToString("D4"), i));
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        var records = (await repository.GetAllTestRunRecords()).ToArray();
        await Assert.That(records.Length).IsEqualTo(1201);
        await Assert.That(records[0].RecordId).IsEqualTo("1200");
        await repository.GetAllTestRunRecords();
        await Assert.That(store.Loads).IsEqualTo(3);
    }

    [Test]
    public async Task Cancelled_history_load_releases_semaphore_and_can_retry()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IEnumerable<TestRunSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PersistentStore { Read = () => { started.TrySetResult(); return release.Task; } };
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        using var cancellation = new CancellationTokenSource();
        var loading = repository.GetAllTestRunRecords(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var waitingCancellation = new CancellationTokenSource();
        var deletion = repository.DeleteReport("old", waitingCancellation.Token);
        waitingCancellation.Cancel();
        await Assert.That(async () => await deletion).Throws<OperationCanceledException>();
        cancellation.Cancel();
        await Assert.That(async () => await loading).Throws<OperationCanceledException>();
        release.TrySetResult([]);
        store.Read = null;
        await Assert.That(await repository.GetAllTestRunRecords()).IsEmpty();
        await repository.DeleteReport("old");
    }

    private static ServiceProvider Services(IBDTestDataStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports();
        if (store != null) services.AddSingleton<IBDTestDataStore>(store);
        var provider = services.BuildServiceProvider();
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
            StartedAtDateTime = Required(Report(id, day).TestTimer).TestsStartedAt
        }));

    private sealed class PersistentStore : IBDTestDataStore, IReportStore, IRunIndexStore
    {
        public Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream?>(Reports.TryGetValue(reportId, out var report)
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BDTestJsonHelper.Serialize(Required(report)))) : null);
        }
        public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
            => Reports[reportId] = Required(await BDTest.Razor.Reports.Serialization.HostedReportJson.DeserializeAsync(utf8Json, cancellationToken));
        public async Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = await GetAllTestRunRecords().WaitAsync(cancellationToken);
            return RunIndexPaging.Apply((records ?? []).Select(record => record.ToRunIndexEntry()), query);
        }
        public Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Records.RemoveAll(record => record.RecordId == entry.RecordId);
            Records.Add(entry.ToTestRunSummary());
            return Task.CompletedTask;
        }
        Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Reports.TryRemove(reportId, out _));
        }
        Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Records.RemoveAll(record => record.RecordId == reportId) != 0);
        }

        public List<TestRunSummary> Records { get; } = [];
        public ConcurrentDictionary<string, BDTest.Contracts.BDTestReport?> Reports { get; } = new();
        public Func<Task<IEnumerable<TestRunSummary>>>? Read { get; set; }
        public int Loads;

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            Interlocked.Increment(ref Loads);
            return Read?.Invoke() ?? Task.FromResult<IEnumerable<TestRunSummary>>(Records.ToArray());
        }

        public Task<BDTest.Contracts.BDTestReport?> GetTestData(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Reports.GetValueOrDefault(id));

        public Task StoreTestData(string id, BDTest.Contracts.BDTestReport? data) { Reports[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Reports.TryRemove(id, out _); return Task.CompletedTask; }
        public Task StoreTestRunRecord(TestRunSummary summary) { Records.Add(summary); return Task.CompletedTask; }
        public Task DeleteTestRunRecord(string id) { Records.RemoveAll(record => record.RecordId == id); return Task.CompletedTask; }
    }
}
