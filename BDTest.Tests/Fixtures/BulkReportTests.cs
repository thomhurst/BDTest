using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
[Arguments(false)]
[Arguments(true)]
public class BulkReportTests(bool flakiness)
{
    private ServiceProvider _services = null!; // Assigned by the per-test setup hook.
    private RecordingDataStore _store = null!; // Assigned by the per-test setup hook.
    private BDTestController _controller = null!; // Assigned by the per-test setup hook.

    [Before(HookType.Test)]
    public void SetUp()
    {
        _store = new RecordingDataStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBDTestReports();
        services.AddSingleton<IBDTestDataStore>(_store);
        services.Configure<BDTestReportOptions>(options => options.ReportCacheSizeLimitBytes = 1024);
        _services = services.BuildServiceProvider();
        _controller = ActivatorUtilities.CreateInstance<BDTestController>(_services);
    }

    [After(HookType.Test)]
    public void TearDown()
    {
        _services.Dispose();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task No_selection_redirects_without_reading_storage(string? ids)
    {
        await Assert.That(await Read(ids)).IsTypeOf<RedirectToActionResult>();
        await Assert.That(_store.ReadIds).IsEmpty();
    }

    [Test]
    [Arguments(" ")]
    [Arguments(",")]
    [Arguments("good,")]
    [Arguments("good,,another")]
    [Arguments("good,../report")]
    [Arguments("good,report/name")]
    [Arguments("good,report\\name")]
    [Arguments("good,report?name")]
    [Arguments("good,report name")]
    [Arguments("good,.")]
    [Arguments("good,..")]
    [Arguments("good,report\n")]
    public async Task Invalid_selection_is_rejected_before_any_read(string ids)
    {
        await Assert.That(await Read(ids)).IsTypeOf<BadRequestObjectResult>();
        await Assert.That(_store.ReadIds).IsEmpty();
    }

    [Test]
    [Arguments(129)]
    [Arguments(6450)]
    public async Task Overlong_id_is_rejected_before_any_read(int length)
    {
        await Assert.That(await Read("good," + new string('a', length))).IsTypeOf<BadRequestObjectResult>();
        await Assert.That(_store.ReadIds).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task More_than_50_entries_are_rejected_even_when_duplicated(bool duplicate)
    {
        var ids = Enumerable.Range(0, 51).Select(i => duplicate ? "same" : $"run-{i}");
        await Assert.That(await Read(string.Join(",", ids))).IsTypeOf<BadRequestObjectResult>();
        await Assert.That(_store.ReadIds).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Fifty_reports_are_returned_without_populating_the_cache(bool maximumLength)
    {
        var ids = Enumerable.Range(0, 50).Select(i => maximumLength ? $"run-{i}".PadRight(128, 'a') : $"run-{i}").ToArray();
        var result = (ViewResult)await Read(string.Join(",", ids));
        await Assert.That(result.ViewName).IsEqualTo(flakiness ? "MultipleTestRunsFlakiness" : "MultipleTestRunsTimes");
        await Assert.That(((IEnumerable<BDTest.Contracts.BDTestReport>)Required(result.Model)).Select(x => x.Id)).IsEquivalentTo((ids).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That(_store.ReadIds).IsEquivalentTo(ids, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        var repository = _services.GetRequiredService<IDataRepository>();
        _store.MissingIds.UnionWith(ids);
        foreach (var id in ids)
        {
            await Assert.That(await repository.GetDataForBulkRead(id, default)).IsNull().Because("Bulk reads must leave every report uncached.");
        }
    }

    [Test]
    public async Task Safe_custom_and_generated_ids_are_supported_and_duplicates_read_once()
    {
        var ids = new[] { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("D"), "build_1.2-Release", new string('a', 128) };
        var result = (ViewResult)await Read(string.Join(",", ids.Concat(ids)));
        await Assert.That(((IEnumerable<BDTest.Contracts.BDTestReport>)Required(result.Model)).Select(x => x.Id)).IsEquivalentTo((ids).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That(_store.ReadIds).IsEquivalentTo(ids, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task Missing_reports_are_omitted_and_all_missing_uses_not_found_view()
    {
        _store.MissingIds.Add("missing");
        var result = (ViewResult)await Read("missing,found");
        await Assert.That(((IEnumerable<BDTest.Contracts.BDTestReport>)Required(result.Model)).Select(x => x.Id)).IsEquivalentTo((new[] { "found" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(((ViewResult)await Read("missing")).ViewName).IsEqualTo("NotFoundMultiple");
    }

    [Test]
    public async Task Bulk_read_reuses_cached_report_without_reapplying_cache_policy()
    {
        var repository = _services.GetRequiredService<IDataRepository>();
        var cached = Required(await repository.GetData("cached", default));
        // Immutable cached reports can be reused without deserializing or reapplying policy.
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var result = (ViewResult)await Read("cached");
            await Assert.That(((IEnumerable<BDTest.Contracts.BDTestReport>)Required(result.Model)).Single()).IsSameReferenceAs(cached);
        }
        await Assert.That(_store.ReadIds).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Single_report_reads_still_populate_the_cache()
    {
        var repository = _services.GetRequiredService<IDataRepository>();
        var report = await repository.GetData("single", CancellationToken.None);
        await Assert.That(await repository.GetData("single", default)).IsSameReferenceAs(report);
        await Assert.That(_store.ReadIds).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Cancelled_request_does_not_start_storage_reads()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await Read("run", cancellation.Token)).ThrowsExactly<OperationCanceledException>();
        await Assert.That(_store.ReadIds).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Storage_reads_are_limited_to_five_and_receive_request_cancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var firstBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Read = async (id, token) =>
        {
            await Assert.That(token).IsEqualTo(cancellation.Token);
            if (_store.ReadIds.Count == 5)
            {
                firstBatchStarted.TrySetResult();
            }
            await release.Task.WaitAsync(token);
            return (new BDTestOutputModel { Id = id }).ToReport();
        };

        var read = Read(string.Join(",", Enumerable.Range(0, 12).Select(i => $"run-{i}")), cancellation.Token);
        try
        {
            await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(_store.ReadIds.Count).IsEqualTo(5);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.That(async () => await read).Throws<OperationCanceledException>();
                await Assert.That(_store.ReadIds.Count).IsEqualTo(5);
            }
            else
            {
                release.SetResult();
                var result = (ViewResult)await read;
                await Assert.That((IEnumerable<BDTest.Contracts.BDTestReport>)Required(result.Model)).Count().IsEqualTo(12);
            }
        }
        finally
        {
            release.TrySetResult();
            try { await read; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
    }

    private Task<IActionResult> Read(string? ids, CancellationToken token = default) => flakiness
        ? _controller.TestRunFlakiness(ids, token)
        : _controller.TestRunTimes(ids, token);

    private sealed class RecordingDataStore : IBDTestDataStore
    {
        public System.Collections.Concurrent.ConcurrentBag<string> ReadIds { get; } = [];
        public HashSet<string> MissingIds { get; } = [];
        public Func<string, CancellationToken, Task<BDTest.Contracts.BDTestReport?>>? Read { get; set; }
        public Task<BDTest.Contracts.BDTestReport?> GetTestData(string id, CancellationToken cancellationToken)
        {
            ReadIds.Add(id);
            return Read?.Invoke(id, cancellationToken)
                ?? Task.FromResult<BDTest.Contracts.BDTestReport?>(MissingIds.Contains(id) ? null : new BDTest.Contracts.BDTestReport(1, id, "tests"));
        }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreTestData(string id, BDTest.Contracts.BDTestReport? data) => throw new NotSupportedException();
        public Task DeleteTestData(string id) => throw new NotSupportedException();
        public Task StoreTestRunRecord(TestRunSummary summary) => throw new NotSupportedException();
        public Task DeleteTestRunRecord(string id) => throw new NotSupportedException();
    }
}
