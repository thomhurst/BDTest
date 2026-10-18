#nullable enable
using System.Text.Json;
using BDTest.Helpers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;

namespace BDTest.Razor.Reports.Storage;

/// <summary>One-release bridge for model-based stores. Legacy operations cannot interrupt in-flight I/O.</summary>
public sealed class LegacyDataStoreAdapter(IBDTestDataStore store) : IReportStore, IRunIndexStore
{
    public async Task<Stream?> OpenReadAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = await store.GetTestData(reportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (model == null) return null;
        var stream = new MemoryStream();
        try
        {
            await JsonSerializer.SerializeAsync(stream, model, BDTestJsonContext.Default.BDTestOutputModel, cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public async Task WriteAsync(string reportId, Stream utf8Json, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = await BDTestJsonHelper.DeserializeAsync(utf8Json, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await store.StoreTestData(reportId, model);
        cancellationToken.ThrowIfCancellationRequested();
    }

    async Task<bool> IReportStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = await store.GetTestData(reportId, cancellationToken) != null;
        cancellationToken.ThrowIfCancellationRequested();
        await store.DeleteTestData(reportId);
        cancellationToken.ThrowIfCancellationRequested();
        return exists;
    }

    public async Task<RunIndexPage> ListAsync(RunIndexQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await store.GetAllTestRunRecords();
        cancellationToken.ThrowIfCancellationRequested();
        return RunIndexPaging.Apply((entries ?? Array.Empty<BDTest.Razor.Reports.Models.TestRunSummary>())
            .Select(entry => entry.ToRunIndexEntry()), query);
    }

    public async Task UpsertAsync(RunIndexEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Some legacy stores only add missing records. Remove first to guarantee replacement.
        await store.DeleteTestRunRecord(entry.RecordId);
        cancellationToken.ThrowIfCancellationRequested();
        await store.StoreTestRunRecord(entry.ToTestRunSummary());
        cancellationToken.ThrowIfCancellationRequested();
    }

    async Task<bool> IRunIndexStore.DeleteAsync(string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await store.GetAllTestRunRecords();
        var exists = entries?.Any(entry => entry.RecordId == reportId) == true;
        cancellationToken.ThrowIfCancellationRequested();
        await store.DeleteTestRunRecord(reportId);
        cancellationToken.ThrowIfCancellationRequested();
        return exists;
    }
}
