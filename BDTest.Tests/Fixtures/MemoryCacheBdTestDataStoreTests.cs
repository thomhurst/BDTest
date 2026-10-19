using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class MemoryCacheBdTestDataStoreTests
{
    [Test]
    public async Task First_record_is_visible_immediately()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(cache);
        var record = CreateRecord(1);

        await store.StoreTestRunRecord(record);

        await Assert.That(await store.GetAllTestRunRecords()).IsEquivalentTo(new[] { record }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Every_record_is_visible_in_descending_start_time_order()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(cache);
        var records = new[] { CreateRecord(2), CreateRecord(1), CreateRecord(3) };

        foreach (var record in records)
        {
            await store.StoreTestRunRecord(record);
        }

        await Assert.That(await store.GetAllTestRunRecords()).IsEquivalentTo(records.OrderByDescending(record => record.StartedAtDateTime), EqualityComparer<TestRunSummary>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Duplicate_id_keeps_the_first_record()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(cache);
        var original = CreateRecord(1);
        var duplicate = CreateRecord(2);
        duplicate.RecordId = original.RecordId;

        await store.StoreTestRunRecord(original);
        await store.StoreTestRunRecord(duplicate);

        await Assert.That(await store.GetAllTestRunRecords()).IsEquivalentTo(new[] { original }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Updates_leave_previous_snapshots_unchanged()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(cache);
        var first = CreateRecord(1);
        var second = CreateRecord(2);
        await store.StoreTestRunRecord(first);
        var snapshot = await store.GetAllTestRunRecords();

        await store.StoreTestRunRecord(second);
        await store.DeleteTestRunRecord(Required(first.RecordId));
        await store.DeleteTestRunRecord("missing");

        await Assert.That(snapshot).IsEquivalentTo(new[] { first }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await store.GetAllTestRunRecords()).IsEquivalentTo(new[] { second }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await store.DeleteTestRunRecord(Required(second.RecordId));
        await Assert.That(await store.GetAllTestRunRecords()).IsEmpty();
    }

    [Test]
    public async Task Concurrent_stores_and_deletes_preserve_all_completed_updates()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var store = new MemoryCacheBdTestDataStore(cache);
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
                await store.DeleteTestRunRecord(Required(removed[int.Parse(Required(record.RecordId)) - 100].RecordId));
                var snapshot = (await store.GetAllTestRunRecords()).ToArray();
                await Assert.That(snapshot).Contains(record).Because("Completed additions must remain visible.");
                await Assert.That(snapshot.Select(item => item.StartedAtDateTime)).IsInDescendingOrder();
            });

        await Assert.That(await store.GetAllTestRunRecords()).IsEquivalentTo(added.OrderByDescending(record => record.StartedAtDateTime), EqualityComparer<TestRunSummary>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static TestRunSummary CreateRecord(int index)
    {
        var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = index.ToString();
        record.StartedAtDateTime = new DateTime(2026, 1, 1).AddMinutes(index);
        return record;
    }
}
