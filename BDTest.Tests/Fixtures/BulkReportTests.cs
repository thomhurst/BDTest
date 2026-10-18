using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[TestFixture(false)]
[TestFixture(true)]
public class BulkReportTests(bool flakiness)
{
    private ServiceProvider _services;
    private RecordingDataStore _store;
    private BDTestController _controller;

    [SetUp]
    public void SetUp()
    {
        _store = new RecordingDataStore();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        _services = services.BuildServiceProvider();
        var options = _services.GetRequiredService<BDTestReportServerOptions>();
        options.DataStore = _store;
        options.ReportCacheSizeLimitBytes = 1024;
        _controller = new BDTestController(_services.GetRequiredService<IDataRepository>(), options);
    }

    [TearDown]
    public void TearDown()
    {
        _services.Dispose();
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task No_selection_redirects_without_reading_storage(string ids)
    {
        Assert.That(await Read(ids), Is.TypeOf<RedirectToActionResult>());
        Assert.That(_store.ReadIds, Is.Empty);
    }

    [TestCase(" ")]
    [TestCase(",")]
    [TestCase("good,")]
    [TestCase("good,,another")]
    [TestCase("good,../report")]
    [TestCase("good,report/name")]
    [TestCase("good,report\\name")]
    [TestCase("good,report?name")]
    [TestCase("good,report name")]
    [TestCase("good,.")]
    [TestCase("good,..")]
    [TestCase("good,report\n")]
    public async Task Invalid_selection_is_rejected_before_any_read(string ids)
    {
        Assert.That(await Read(ids), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(_store.ReadIds, Is.Empty);
    }

    [TestCase(129)]
    [TestCase(6450)]
    public async Task Overlong_id_is_rejected_before_any_read(int length)
    {
        Assert.That(await Read("good," + new string('a', length)), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(_store.ReadIds, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task More_than_50_entries_are_rejected_even_when_duplicated(bool duplicate)
    {
        var ids = Enumerable.Range(0, 51).Select(i => duplicate ? "same" : $"run-{i}");
        Assert.That(await Read(string.Join(",", ids)), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(_store.ReadIds, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Fifty_reports_are_returned_without_populating_the_cache(bool maximumLength)
    {
        var ids = Enumerable.Range(0, 50).Select(i => maximumLength ? $"run-{i}".PadRight(128, 'a') : $"run-{i}").ToArray();
        var result = (ViewResult)await Read(string.Join(",", ids));
        Assert.That(result.ViewName, Is.EqualTo(flakiness ? "MultipleTestRunsFlakiness" : "MultipleTestRunsTimes"));
        Assert.That(((IEnumerable<BDTestOutputModel>)result.Model).Select(x => x.Id), Is.EqualTo(ids));
        Assert.That(_store.ReadIds, Is.EquivalentTo(ids));
        var repository = _services.GetRequiredService<IDataRepository>();
        _store.MissingIds.UnionWith(ids);
        foreach (var id in ids)
        {
            Assert.That(await repository.GetDataForBulkRead(id, default), Is.Null,
                "Bulk reads must leave every report uncached.");
        }
    }

    [Test]
    public async Task Safe_custom_and_generated_ids_are_supported_and_duplicates_read_once()
    {
        var ids = new[] { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("D"), "build_1.2-Release", new string('a', 128) };
        var result = (ViewResult)await Read(string.Join(",", ids.Concat(ids)));
        Assert.That(((IEnumerable<BDTestOutputModel>)result.Model).Select(x => x.Id), Is.EqualTo(ids));
        Assert.That(_store.ReadIds, Is.EquivalentTo(ids));
    }

    [Test]
    public async Task Missing_reports_are_omitted_and_all_missing_uses_not_found_view()
    {
        _store.MissingIds.Add("missing");
        var result = (ViewResult)await Read("missing,found");
        Assert.That(((IEnumerable<BDTestOutputModel>)result.Model).Select(x => x.Id), Is.EqualTo(new[] { "found" }));
        Assert.That(((ViewResult)await Read("missing")).ViewName, Is.EqualTo("NotFoundMultiple"));
    }

    [Test]
    public async Task Bulk_read_reuses_cached_report_without_reapplying_cache_policy()
    {
        var repository = _services.GetRequiredService<IDataRepository>();
        var cached = await repository.GetData("cached", default);
        // Re-caching this now-oversized model would remove it from the bounded cache.
        cached.Tag = new string('x', 2000);
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var result = (ViewResult)await Read("cached");
            Assert.That(((IEnumerable<BDTestOutputModel>)result.Model).Single(), Is.SameAs(cached));
        }
        Assert.That(_store.ReadIds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Single_report_reads_still_populate_the_cache()
    {
        var repository = _services.GetRequiredService<IDataRepository>();
        var report = await repository.GetData("single", CancellationToken.None);
        Assert.That(await repository.GetData("single", default), Is.SameAs(report));
        Assert.That(_store.ReadIds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Cancelled_request_does_not_start_storage_reads()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Read("run", cancellation.Token));
        Assert.That(_store.ReadIds, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Storage_reads_are_limited_to_five_and_receive_request_cancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var firstBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Read = async (id, token) =>
        {
            Assert.That(token, Is.EqualTo(cancellation.Token));
            if (_store.ReadIds.Count == 5)
            {
                firstBatchStarted.TrySetResult();
            }
            await release.Task.WaitAsync(token);
            return new BDTestOutputModel { Id = id };
        };

        var read = Read(string.Join(",", Enumerable.Range(0, 12).Select(i => $"run-{i}")), cancellation.Token);
        try
        {
            await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(_store.ReadIds.Count, Is.EqualTo(5));
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.CatchAsync<OperationCanceledException>(async () => await read);
                Assert.That(_store.ReadIds.Count, Is.EqualTo(5));
            }
            else
            {
                release.SetResult();
                var result = (ViewResult)await read;
                Assert.That((IEnumerable<BDTestOutputModel>)result.Model, Has.Count.EqualTo(12));
            }
        }
        finally
        {
            release.TrySetResult();
            try { await read; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
    }

    private Task<IActionResult> Read(string ids, CancellationToken token = default) => flakiness
        ? _controller.TestRunFlakiness(ids, token)
        : _controller.TestRunTimes(ids, token);

    private sealed class RecordingDataStore : IBDTestDataStore
    {
        public System.Collections.Concurrent.ConcurrentBag<string> ReadIds { get; } = [];
        public HashSet<string> MissingIds { get; } = [];
        public Func<string, CancellationToken, Task<BDTestOutputModel>> Read { get; set; }
        public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
        {
            ReadIds.Add(id);
            return Read?.Invoke(id, cancellationToken)
                ?? Task.FromResult(MissingIds.Contains(id) ? null : new BDTestOutputModel { Id = id });
        }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult(Enumerable.Empty<TestRunSummary>());
        public Task StoreTestData(string id, BDTestOutputModel data) => throw new NotSupportedException();
        public Task DeleteTestData(string id) => throw new NotSupportedException();
        public Task StoreTestRunRecord(TestRunSummary summary) => throw new NotSupportedException();
        public Task DeleteTestRunRecord(string id) => throw new NotSupportedException();
    }
}
