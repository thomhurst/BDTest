using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class DataRepositoryDeletionTests
{
    private const string Id = "00112233445566778899aabbccddeeff";
    private ServiceProvider _services = null!; // Assigned by the per-test setup hook.
    private FaultingStore _persistent = null!; // Assigned by the per-test setup hook.
    private FaultingStore _cache = null!; // Assigned by the per-test setup hook.
    private IDataRepository _repository = null!; // Assigned by the per-test setup hook.
    private readonly List<string> _steps = [];

    [Before(HookType.Test)]
    public async Task SetUp()
    {
        _steps.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports();
        _services = services.BuildServiceProvider();
        _persistent = new FaultingStore("persistent", _steps);
        _cache = new FaultingStore("cache", _steps);
        _repository = CreateRepository();
        await _repository.StoreData(Id, (new BDTestOutputModel { Id = Id }).ToReport());
        await _repository.GetAllTestRunRecords();
    }

    private DataRepository CreateRepository(IBDTestAttachmentStore? attachments = null, ILogger<DataRepository>? logger = null) =>
        new(_cache, _services.GetRequiredService<ReportAttachmentGate>(),
            reportStore: _persistent, runIndexStore: _persistent, attachmentStore: attachments, logger: logger);

    [After(HookType.Test)]
    public void TearDown()
    {
        _cache.Dispose();
        _persistent.Dispose();
        _services.Dispose();
    }

    [Test]
    public async Task Delete_removes_index_before_data_and_is_idempotent()
    {
        await _repository.DeleteReport(Id);
        await _repository.DeleteReport(Id);
        await Assert.That(_steps).IsEquivalentTo(Enumerable.Repeat(new[]
        {
            "persistent-index", "persistent-report", "cache-index", "cache-report"
        }, 2).SelectMany(steps => steps), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await _repository.GetData(Id, default)).IsNull();
        await Assert.That(await _repository.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(await _persistent.GetAllTestRunRecords()).IsEmpty();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    [Arguments("missing", "missing")]
    [Arguments("missing\rforged", "missing\\rforged")]
    [Arguments("missing\nforged", "missing\\nforged")]
    [Arguments("missing\r\nforged", "missing\\r\\nforged")]
    public async Task Missing_report_log_escapes_line_breaks(string id, string loggedId)
    {
        var logger = new RecordingLogger();
        _repository = CreateRepository(logger: logger);

        await _repository.DeleteReport(id);

        await Assert.That(logger.Message).IsEqualTo($"Report {loggedId} did not exist during deletion.");

        await Assert.That(logger.Message).DoesNotContain("\r").And.DoesNotContain("\n");
    }

    [Test]
    [Arguments("persistent-index")]
    [Arguments("persistent-report")]
    [Arguments("cache-index")]
    [Arguments("cache-report")]
    public async Task Every_failure_attempts_both_cache_invalidations_and_can_retry(string step)
    {
        var failure = new IOException(step);
        var failingStore = step.StartsWith("persistent") ? _persistent : _cache;
        failingStore.Failures[step] = failure;
        var thrown = await Assert.That(() => _repository.DeleteReport(Id)).ThrowsExactly<IOException>();
        await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(_steps.TakeLast(2)).IsEquivalentTo(new[] { "cache-index", "cache-report" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        if (step == "persistent-index")
        {
            await Assert.That(_steps).DoesNotContain("persistent-report");
            await Assert.That(await _persistent.GetTestData(Id, default)).IsNotNull();
            await Assert.That(await _persistent.GetAllTestRunRecords()).Count().IsEqualTo(1);
        }
        else
        {
            await Assert.That(await _persistent.GetAllTestRunRecords()).IsEmpty();
        }
        if (step != "cache-index") await Assert.That(await _cache.GetAllTestRunRecords()).IsEmpty();
        if (step != "cache-report") await Assert.That(await _cache.GetTestData(Id, default)).IsNull();
        if (step == "persistent-report") await Assert.That(await _persistent.GetTestData(Id, default)).IsNotNull();

        failingStore.Failures.Clear();
        await _repository.DeleteReport(Id);
        await Assert.That(await _repository.GetData(Id, default)).IsNull();
        await Assert.That(await _repository.GetAllTestRunRecords()).IsEmpty();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    public async Task Attachment_failure_keeps_report_data_and_retries_cleanup()
    {
        var failure = new IOException("attachments unavailable");
        var attachments = new FailingAttachments { Failure = failure };
        _repository = CreateRepository(attachments);
        await Assert.That(await Assert.That(() => _repository.DeleteReport(Id)).ThrowsExactly<IOException>()).IsSameReferenceAs(failure);
        await Assert.That(_steps).IsEquivalentTo(new[] { "persistent-index", "cache-index", "cache-report" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await _persistent.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(await _persistent.GetTestData(Id, default)).IsNotNull();
        await Assert.That(await _cache.GetTestData(Id, default)).IsNull();
        attachments.Failure = null;
        await _repository.DeleteReport(Id);
        await Assert.That(attachments.Attempts).IsEqualTo(2);
        await Assert.That(await _repository.GetData(Id, default)).IsNull();
    }

    [Test]
    public async Task Cache_failures_do_not_mask_original_storage_failure()
    {
        var original = new IOException("index unavailable");
        _persistent.Failures["persistent-index"] = original;
        _cache.Failures["cache-index"] = new InvalidOperationException("cache index unavailable");
        _cache.Failures["cache-report"] = new InvalidOperationException("cache report unavailable");
        await Assert.That(await Assert.That(() => _repository.DeleteReport(Id)).ThrowsExactly<IOException>()).IsSameReferenceAs(original);
        await Assert.That(_steps).IsEquivalentTo(new[] { "persistent-index", "cache-index", "cache-report" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Cancellation_during_persistent_delete_still_invalidates_both_cache_entries()
    {
        using var cancellation = new CancellationTokenSource();
        _persistent.BeforeDelete = () => cancellation.Cancel();
        await Assert.That(() => _repository.DeleteReport(Id, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(_steps).IsEquivalentTo(new[] { "persistent-index", "cache-index", "cache-report" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await _cache.GetAllTestRunRecords()).IsEmpty();
        await Assert.That(await _cache.GetTestData(Id, default)).IsNull();
    }

    [Test]
    public async Task In_flight_read_cannot_repopulate_cache_after_delete()
    {
        await ((IReportStore)_cache).DeleteAsync(Id, default);
        _steps.Clear();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _persistent.AfterOpen = async () => { started.TrySetResult(); await release.Task; };
        var read = _repository.GetData(Id, default);
        Task deletion = Task.CompletedTask;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            deletion = _repository.DeleteReport(Id);
            await Assert.That(deletion.IsCompleted).IsFalse();
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(read, deletion).WaitAsync(TimeSpan.FromSeconds(10));
        }
        await Assert.That(await _cache.GetTestData(Id, default)).IsNull();
        await Assert.That(await _repository.GetData(Id, default)).IsNull();
    }

    [Test]
    public async Task Overlapping_page_reads_share_one_projection_and_persistent_read()
    {
        await ((IReportStore)_cache).DeleteAsync(Id, default);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opens = 0;
        _persistent.AfterOpen = async () =>
        {
            Interlocked.Increment(ref opens);
            started.TrySetResult();
            await release.Task;
        };
        var first = _repository.GetReportPages(Id, default);
        var second = first;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = _repository.GetReportPages(Id, default);
            await Assert.That(first.IsCompleted).IsFalse();
            await Assert.That(second.IsCompleted).IsFalse();
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }
        await Assert.That(await first).IsNotNull();
        await Assert.That(await second).IsSameReferenceAs(await first);
        await Assert.That(opens).IsEqualTo(1);
    }

    private sealed class RecordingLogger : ILogger<DataRepository>
    {
        public string? Message { get; private set; }
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Message = formatter(state, exception);
    }

    private sealed class FailingAttachments : IBDTestAttachmentStore
    {
        public Exception? Failure { get; set; }
        public int Attempts { get; private set; }
        public Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Failure != null) throw Failure;
            return Task.CompletedTask;
        }
        public Task<Stream?> OpenReadAsync(string reportId, string attachmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task WriteAsync(string reportId, string attachmentId, Stream content, long length, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FaultingStore(string name, List<string> steps) : IMemoryCacheBdTestDataStore, IDisposable
    {
        private readonly MemoryCacheBdTestDataStore _inner = new(new MemoryCache(new MemoryCacheOptions()));
        public Dictionary<string, Exception> Failures { get; } = [];
        public Action? BeforeDelete { get; set; }
        public Func<Task>? AfterOpen { get; set; }
        public async Task<Stream?> OpenReadAsync(string id, CancellationToken token)
        {
            var stream = await _inner.OpenReadAsync(id, token);
            if (AfterOpen != null) await AfterOpen();
            return stream;
        }
        public Task WriteAsync(string id, Stream stream, CancellationToken token) => _inner.WriteAsync(id, stream, token);
        public Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken token) => _inner.ListAsync(query, token);
        public Task UpsertAsync(RunIndexEntry entry, CancellationToken token) => _inner.UpsertAsync(entry, token);
        async Task<bool> IReportStore.DeleteAsync(string id, CancellationToken token)
        {
            Check("report", token);
            return await ((IReportStore)_inner).DeleteAsync(id, token);
        }
        async Task<bool> IRunIndexStore.DeleteAsync(string id, CancellationToken token)
        {
            Check("index", token);
            return await ((IRunIndexStore)_inner).DeleteAsync(id, token);
        }
        private void Check(string kind, CancellationToken token)
        {
            var step = name + "-" + kind;
            steps.Add(step);
            BeforeDelete?.Invoke();
            token.ThrowIfCancellationRequested();
            if (Failures.TryGetValue(step, out var failure)) throw failure;
        }
        public Task<BDTest.Contracts.BDTestReport?> GetTestData(string id, CancellationToken token) => _inner.GetTestData(id, token);
        public Task<BDTest.Contracts.BDTestReport?> GetTestDataAndRefreshRetention(string id, CancellationToken token) =>
            ((IMemoryCacheBdTestDataStore)_inner).GetTestDataAndRefreshRetention(id, token);
        public Task<BDTest.Razor.Reports.Models.ViewModels.ReportPageModels?> GetReportPagesAndRefreshRetention(string id, CancellationToken token) =>
            ((IMemoryCacheBdTestDataStore)_inner).GetReportPagesAndRefreshRetention(id, token);
        public Task StoreTestData(string id, BDTest.Contracts.BDTestReport? model) => _inner.StoreTestData(id, model);
        public Task DeleteTestData(string id) => _inner.DeleteTestData(id);
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => _inner.GetAllTestRunRecords();
        public Task StoreTestRunRecord(TestRunSummary summary) => _inner.StoreTestRunRecord(summary);
        public Task DeleteTestRunRecord(string id) => _inner.DeleteTestRunRecord(id);
        public void Dispose() => _inner.Dispose();
    }
}
