using System.Collections.Concurrent;
using System.Net;
using System.Text;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportStoreTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Oversized_memory_store_writes_fail_without_removing_existing_reports(bool hasPersistentStorage)
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        var options = new BDTestReportOptions { ReportCacheSizeLimitBytes = 8 };
        using var store = new MemoryCacheBdTestDataStore(hostCache, Options.Create(options),
            hasPersistentStorage: hasPersistentStorage);
        using var original = Bytes("original");
        await store.WriteAsync("run", original, default);

        foreach (var id in new[] { "new", "run" })
        {
            using var oversized = Bytes(new string('x', 81921));
            await Assert.That(async () =>
                await store.WriteAsync(id, oversized, default)).Throws<InvalidOperationException>();
            await Assert.That(oversized.CanRead).IsTrue();
        }

        await Assert.That(await store.OpenReadAsync("new", default)).IsNull();
        await using var retained = await store.OpenReadAsync("run", default);
        using var reader = new StreamReader(Required(retained));
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("original");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    // Preserve the wall-clock probe's budget independently of unrelated parallel work.
    [NotInParallel]
    public async Task Raw_memory_reads_extend_retention_for_models_and_bytes(bool bytes)
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        var options = new BDTestReportOptions { ReportCacheRetention = TimeSpan.FromSeconds(2) };
        using var store = new MemoryCacheBdTestDataStore(hostCache, Options.Create(options));
        foreach (var id in new[] { "active", "idle" })
        {
            var model = new BDTestOutputModel { Id = id };
            if (bytes)
            {
                using var input = Bytes(BDTestJsonHelper.Serialize(model));
                await store.WriteAsync(id, input, default);
            }
            else await store.StoreTestData(id, model);
        }

        await Task.Delay(1200);
        await using (var download = await store.OpenReadAsync("active", default))
            await Assert.That(download).IsNotNull();
        await Task.Delay(1200);

        await Assert.That(await store.GetTestData("active", default)).IsNotNull();
        await Assert.That(await store.GetTestData("idle", default)).IsNull();
    }

    [Test]
    public async Task Memory_store_preserves_bytes_and_leaves_input_open()
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(hostCache);
        // Deliberately not a report: the byte store must not interpret JSON.
        const string json = "  { \"unknown\": true } ";
        using var input = Bytes(json);
        await store.WriteAsync("run", input, default);
        await Assert.That(input.CanRead).IsTrue();
        await using var first = await store.OpenReadAsync("run", default);
        using var reader = new StreamReader(Required(first));
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo(json);
        using var replacement = Bytes("replacement");
        await store.WriteAsync("run", replacement, default);
        await using var second = await store.OpenReadAsync("run", default);
        using var secondReader = new StreamReader(Required(second));
        await Assert.That(await secondReader.ReadToEndAsync()).IsEqualTo("replacement");
        await Assert.That(await ((IReportStore)store).DeleteAsync("run", default)).IsTrue();
        await Assert.That(await ((IReportStore)store).DeleteAsync("run", default)).IsFalse();
    }

    [Test]
    public async Task Memory_index_filters_orders_and_continues_across_changes()
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(hostCache);
        foreach (var id in new[] { "d", "c", "b", "a" })
            await store.UpsertAsync(Entry(id), default);
        await store.UpsertAsync(new RunIndexEntry { RecordId = "other", BranchName = "other" }, default);
        var first = await store.ListAsync(Query(), default);
        await Assert.That(first.Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "a", "b" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        // Inserting an earlier key and removing the cursor must not skip the next page.
        await store.UpsertAsync(Entry("0"), default);
        await ((IRunIndexStore)store).DeleteAsync("b", default);
        var second = await store.ListAsync(Query(first.ContinuationToken), default);
        await Assert.That(second.Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "c", "d" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second.ContinuationToken).IsNull();
        await store.UpsertAsync(new RunIndexEntry { RecordId = "a", BranchName = "other" }, default);
        await Assert.That((await store.ListAsync(Query(), default)).Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "0", "c" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(() => RunIndexPaging.Apply([], new RunIndexQuery
            { BranchName = "other", ContinuationToken = first.ContinuationToken })).ThrowsExactly<ArgumentException>();
        await Assert.That(() => RunIndexPaging.Apply([], Query("bad-token"))).ThrowsExactly<ArgumentException>();
        await Assert.That(() => RunIndexPaging.Apply([], new RunIndexQuery { PageSize = 0 })).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Store_operations_reject_cancellation_before_mutating(bool legacy)
    {
        using var hostCache = new MemoryCache(new MemoryCacheOptions());
        using var memory = new MemoryCacheBdTestDataStore(hostCache);
        var old = new LegacyStore();
        var adapter = new LegacyDataStoreAdapter(old);
        IReportStore reports = legacy ? adapter : memory;
        IRunIndexStore index = legacy ? adapter : memory;
        var token = new CancellationToken(canceled: true);
        using var input = Bytes("{}");
        await Assert.That(async () => await reports.OpenReadAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await reports.WriteAsync("run", input, token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await reports.DeleteAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await index.ListAsync(new RunIndexQuery(), token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await index.UpsertAsync(Entry("run"), token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await index.DeleteAsync("run", token)).Throws<OperationCanceledException>();
        await Assert.That(old.Calls).IsZero();
        await Assert.That(input.CanRead).IsTrue();
    }

    [Test]
    public async Task Legacy_adapter_round_trips_reports_and_pages_existing_summaries()
    {
        var store = new LegacyStore();
        var adapter = new LegacyDataStoreAdapter(store);
        using var input = Bytes("{\"Id\":\"run\",\"Tag\":\"legacy\"}");
        await adapter.WriteAsync("run", input, default);
        await Assert.That(input.CanRead).IsTrue();
        await using var output = await adapter.OpenReadAsync("run", default);
        await Assert.That(Required(await BDTestJsonHelper.DeserializeAsync(Required(output))).Tag).IsEqualTo("legacy");
        foreach (var id in new[] { "d", "c", "b", "a" }) await adapter.UpsertAsync(Entry(id), default);
        var first = await adapter.ListAsync(Query(), default);
        var second = await adapter.ListAsync(Query(first.ContinuationToken), default);
        await Assert.That(first.Entries.Concat(second.Entries).Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "a", "b", "c", "d" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await adapter.UpsertAsync(new RunIndexEntry { RecordId = "a", BranchName = "other" }, default);
        await Assert.That((await adapter.ListAsync(Query(), default)).Entries.Select(entry => entry.RecordId)).IsEquivalentTo((new[] { "b", "c" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await ((IReportStore)adapter).DeleteAsync("run", default)).IsTrue();
        await Assert.That(await adapter.OpenReadAsync("missing", default)).IsNull();
        await Assert.That(await ((IRunIndexStore)adapter).DeleteAsync("a", default)).IsTrue();
        await Assert.That(await ((IRunIndexStore)adapter).DeleteAsync("a", default)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Legacy_adapter_does_not_delete_if_cancelled_during_existence_read(bool index)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new LegacyStore { OnRead = cancellation.Cancel };
        var adapter = new LegacyDataStoreAdapter(store);
        await Assert.That(async () =>
        {
            if (index) await ((IRunIndexStore)adapter).DeleteAsync("run", cancellation.Token);
            else await ((IReportStore)adapter).DeleteAsync("run", cancellation.Token);
        }).Throws<OperationCanceledException>();
        await Assert.That(store.Deletes).IsZero();
    }

    [Test]
    public async Task Raw_HTTP_download_preserves_storage_bytes_and_MVC_disposes_stream()
    {
        var store = new ByteStore();
        // If the endpoint tried deserializing, this payload would fail report parsing.
        const string json = "  { \"unrecognised\": [1, 2] }\n";
        store.Reports["run"] = Encoding.UTF8.GetBytes(json);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports(options => options.AllowAnonymousReports = true);
        builder.Services.AddSingleton<IReportStore>(store);
        builder.Services.AddSingleton<IRunIndexStore>(store);
        await using var app = builder.Build();
        app.UseBDTestReportServer();
        app.MapBDTestReports();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await client.GetAsync("bdtest/report/run/raw-json-data");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo(json);
        await Assert.That(Required(response.Content.Headers.ContentType).MediaType).IsEqualTo("application/json");
        await Required(store.LastRead).Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var missing = await client.GetAsync("bdtest/report/missing/raw-json-data");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(store.Lists).IsZero();
    }

    [Test]
    [Arguments("{\"$id\":\"1\",\"Id\":\"run\",\"Tag\":\"old\",\"Scenarios\":{\"$values\":[]}}", false)]
    [Arguments("invalid-json", true)]
    public async Task Repository_disposes_storage_stream_after_model_read(string json, bool fails)
    {
        var store = new ByteStore();
        store.Reports["run"] = Encoding.UTF8.GetBytes(json);
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        if (fails) await Assert.That(async () => await repository.GetData("run", default)).Throws<System.Text.Json.JsonException>();
        else await Assert.That(Required(await repository.GetData("run", default)).Tag).IsEqualTo("old");
        await Assert.That(Required(store.LastRead).Disposed.Task.IsCompleted).IsTrue();
    }

    [Test]
    public async Task Repository_disposes_stream_if_cancelled_after_opening()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new ByteStore { AfterOpen = cancellation.Cancel };
        store.Reports["run"] = Encoding.UTF8.GetBytes("{}");
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        await Assert.That(async () => await repository.GetData("run", cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(Required(store.LastRead).Disposed.Task.IsCompleted).IsTrue();
    }

    [Test]
    public async Task Repository_passes_tokens_to_writes_index_and_deletes()
    {
        var store = new ByteStore();
        using var services = Services(store);
        var repository = services.GetRequiredService<IDataRepository>();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        await repository.StoreData("run", new BDTestOutputModel { Id = "run" }, token);
        await Assert.That(store.Tokens.All(actual => actual == token)).IsTrue();
        await Assert.That(store.Tokens.Count).IsEqualTo(2);
        await repository.GetAllTestRunRecords(token);
        await Assert.That(store.Tokens.Last()).IsEqualTo(token);
        await repository.DeleteReport("run", token);
        await Assert.That(store.Reports).IsEmpty();
        await Assert.That(store.Entries).IsEmpty();
        await Assert.That(store.Tokens.All(actual => actual == token)).IsTrue();
        cancellation.Cancel();
        await Assert.That(async () => await repository.StoreData("cancelled", new(), token)).Throws<OperationCanceledException>();
        await Assert.That(store.Reports).IsEmpty();
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
    private static RunIndexEntry Entry(string id) => new()
    {
        RecordId = id, StartedAtDateTime = new DateTime(2026, 1, 1), BranchName = "main", Environment = "ci", Tag = "smoke"
    };
    private static RunIndexQuery Query(string? continuation = null) => new()
    {
        PageSize = 2, BranchName = "main", Environment = "ci", Tag = "smoke", ContinuationToken = continuation
    };
    private static ServiceProvider Services(ByteStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports();
        if (store != null)
        {
            services.AddSingleton<IReportStore>(store);
            services.AddSingleton<IRunIndexStore>(store);
        }
        return services.BuildServiceProvider();
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void Dispose(bool disposing) { base.Dispose(disposing); Disposed.TrySetResult(); }
    }

    private sealed class ByteStore : IReportStore, IRunIndexStore
    {
        public ConcurrentDictionary<string, byte[]> Reports { get; } = new();
        public ConcurrentDictionary<string, RunIndexEntry> Entries { get; } = new();
        public List<CancellationToken> Tokens { get; } = [];
        public TrackingStream? LastRead { get; private set; }
        public int Lists;
        public Action? AfterOpen;
        public Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tokens.Add(cancellationToken);
            Stream? stream = Reports.TryGetValue(reportId, out var bytes) ? LastRead = new TrackingStream(bytes) : null;
            AfterOpen?.Invoke();
            return Task.FromResult(stream);
        }
        public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            using var output = new MemoryStream();
            await utf8Json.CopyToAsync(output, cancellationToken);
            Reports[reportId] = output.ToArray();
        }
        public Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tokens.Add(cancellationToken);
            Lists++;
            return Task.FromResult(RunIndexPaging.Apply(Entries.Values, query));
        }
        public Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tokens.Add(cancellationToken);
            Entries[Required(entry.RecordId)] = entry;
            return Task.CompletedTask;
        }
        Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Tokens.Add(cancellationToken);
            return Task.FromResult(Reports.TryRemove(reportId, out _));
        }
        Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Tokens.Add(cancellationToken);
            return Task.FromResult(Entries.TryRemove(reportId, out _));
        }
    }

    private sealed class LegacyStore : IBDTestDataStore
    {
        private readonly Dictionary<string, BDTestOutputModel?> _reports = new();
        private readonly Dictionary<string, TestRunSummary> _summaries = new();
        public int Calls;
        public int Deletes;
        public Action? OnRead;
        public Task<BDTestOutputModel?> GetTestData(string id, CancellationToken cancellationToken)
        { Calls++; OnRead?.Invoke(); return Task.FromResult(_reports.GetValueOrDefault(id)); }
        public Task StoreTestData(string id, BDTestOutputModel? data) { Calls++; _reports[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Deletes++; _reports.Remove(id); return Task.CompletedTask; }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        { Calls++; OnRead?.Invoke(); return Task.FromResult<IEnumerable<TestRunSummary>>(_summaries.Values.ToArray()); }
        public Task StoreTestRunRecord(TestRunSummary summary) { Calls++; _summaries.TryAdd(Required(summary.RecordId), summary); return Task.CompletedTask; }
        public Task DeleteTestRunRecord(string id) { Deletes++; _summaries.Remove(id); return Task.CompletedTask; }
    }
}
