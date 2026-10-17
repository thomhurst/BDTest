using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class MemoryCacheBdTestDataStoreTests
{
    [Test]
    public async Task First_record_is_visible_immediately()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheBdTestDataStore(cache);
        var record = CreateRecord(1);

        await store.StoreTestRunRecord(record);

        Assert.That(await store.GetAllTestRunRecords(), Is.EqualTo(new[] { record }));
    }

    [Test]
    public async Task Every_record_is_visible_in_descending_start_time_order()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheBdTestDataStore(cache);
        var records = new[] { CreateRecord(2), CreateRecord(1), CreateRecord(3) };

        foreach (var record in records)
        {
            await store.StoreTestRunRecord(record);
        }

        Assert.That(await store.GetAllTestRunRecords(),
            Is.EqualTo(records.OrderByDescending(record => record.StartedAtDateTime)));
    }

    [Test]
    public async Task Duplicate_id_keeps_the_first_record()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheBdTestDataStore(cache);
        var original = CreateRecord(1);
        var duplicate = CreateRecord(2);
        duplicate.RecordId = original.RecordId;

        await store.StoreTestRunRecord(original);
        await store.StoreTestRunRecord(duplicate);

        Assert.That(await store.GetAllTestRunRecords(), Is.EqualTo(new[] { original }));
    }

    [Test]
    public async Task Updates_leave_previous_snapshots_unchanged()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheBdTestDataStore(cache);
        var first = CreateRecord(1);
        var second = CreateRecord(2);
        await store.StoreTestRunRecord(first);
        var snapshot = await store.GetAllTestRunRecords();

        await store.StoreTestRunRecord(second);
        await store.DeleteTestRunRecord(first.RecordId);
        await store.DeleteTestRunRecord("missing");

        Assert.That(snapshot, Is.EqualTo(new[] { first }));
        Assert.That(await store.GetAllTestRunRecords(), Is.EqualTo(new[] { second }));
        await store.DeleteTestRunRecord(second.RecordId);
        Assert.That(await store.GetAllTestRunRecords(), Is.Empty);
    }

    [Test]
    public async Task Concurrent_stores_and_deletes_preserve_all_completed_updates()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheBdTestDataStore(cache);
        var removed = Enumerable.Range(0, 100).Select(CreateRecord).ToArray();
        var added = Enumerable.Range(100, 100).Select(CreateRecord).ToArray();
        foreach (var record in removed)
        {
            await store.StoreTestRunRecord(record);
        }

        await Parallel.ForEachAsync(added, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (record, _) =>
            {
                await store.StoreTestRunRecord(record);
                await store.DeleteTestRunRecord(removed[int.Parse(record.RecordId) - 100].RecordId);
                var snapshot = (await store.GetAllTestRunRecords()).ToArray();
                Assert.That(snapshot, Does.Contain(record), "Completed additions must remain visible.");
                Assert.That(snapshot.Select(item => item.StartedAtDateTime), Is.Ordered.Descending);
            });

        Assert.That(await store.GetAllTestRunRecords(),
            Is.EqualTo(added.OrderByDescending(record => record.StartedAtDateTime)));
    }

    private static TestRunSummary CreateRecord(int index)
    {
        var record = JsonConvert.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = index.ToString();
        record.StartedAtDateTime = new DateTime(2026, 1, 1).AddMinutes(index);
        return record;
    }
}
