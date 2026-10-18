using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BDTest.ReportGenerator.RazorServer;

namespace BDTest.ReportGenerator.Tests;

public class StorageInitializationTests
{
    [Test]
    public async Task Blob_initialization_awaits_both_containers_and_passes_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var summaries = new RecordingContainer();
        var reports = new RecordingContainer();
        var store = new AzureStorageDataStore(summaries, reports);
        await Assert.That(summaries.Calls + reports.Calls).IsZero();

        var initialization = store.InitializeAsync(cancellation.Token);
        await Assert.That(initialization.IsCompleted).IsFalse();
        await Assert.That(summaries.Calls).IsEqualTo(1);
        summaries.Completion.SetResult(null);
        await reports.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(initialization.IsCompleted).IsFalse();
        reports.Completion.SetResult(null);
        await initialization;

        await Assert.That(reports.Calls).IsEqualTo(1);
        await Assert.That(summaries.Token).IsEqualTo(cancellation.Token);
        await Assert.That(reports.Token).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Blob_initialization_propagates_container_creation_failure()
    {
        var summaries = new RecordingContainer();
        var reports = new RecordingContainer();
        var failure = new RequestFailedException(403, "Access denied");
        summaries.Completion.SetException(failure);
        var exception = await Assert.ThrowsExactlyAsync<RequestFailedException>(() =>
            new AzureStorageDataStore(summaries, reports).InitializeAsync());
        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(reports.Calls).IsZero();
    }

    private sealed class RecordingContainer : BlobContainerClient
    {
        public int Calls;
        public CancellationToken Token;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Azure.Response<BlobContainerInfo>?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<Azure.Response<BlobContainerInfo>?> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            Started.TrySetResult();
            return Completion.Task.WaitAsync(cancellationToken);
        }
    }

}
