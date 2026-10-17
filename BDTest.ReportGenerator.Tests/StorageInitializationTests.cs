using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;

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
        Assert.That(summaries.Calls + reports.Calls, Is.Zero);

        var initialization = store.InitializeAsync(cancellation.Token);
        Assert.That(initialization.IsCompleted, Is.False);
        Assert.That(summaries.Calls, Is.EqualTo(1));
        summaries.Completion.SetResult(null);
        await reports.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(initialization.IsCompleted, Is.False);
        reports.Completion.SetResult(null);
        await initialization;

        Assert.That(reports.Calls, Is.EqualTo(1));
        Assert.That(summaries.Token, Is.EqualTo(cancellation.Token));
        Assert.That(reports.Token, Is.EqualTo(cancellation.Token));
    }

    [Test]
    public async Task Blob_initialization_propagates_container_creation_failure()
    {
        var summaries = new RecordingContainer();
        var reports = new RecordingContainer();
        var failure = new RequestFailedException(403, "Access denied");
        summaries.Completion.SetException(failure);
        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            new AzureStorageDataStore(summaries, reports).InitializeAsync());
        Assert.That(exception, Is.SameAs(failure));
        Assert.That(reports.Calls, Is.Zero);
    }

    [Test]
    public async Task Cosmos_constructor_does_no_network_work_and_initialization_observes_failure()
    {
        using var client = new RecordingCosmosClient();
        using var store = new CosmosBDTestDataStore(client);
        using var cancellation = new CancellationTokenSource();
        Assert.That(client.Creations, Is.Zero);
        Assert.That(client.Containers, Is.EqualTo(new[] { "bdtest-reports/test-data", "bdtest-reports/test-run-summary" }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync(cancellation.Token));
        Assert.That(exception, Is.SameAs(client.Failure));
        Assert.That(client.Creations, Is.EqualTo(1));
        Assert.That(client.Token, Is.EqualTo(cancellation.Token));
    }

    private sealed class RecordingContainer : BlobContainerClient
    {
        public int Calls;
        public CancellationToken Token;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Azure.Response<BlobContainerInfo>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<Azure.Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string> metadata = null,
            BlobContainerEncryptionScopeOptions encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            Started.TrySetResult();
            return Completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class RecordingCosmosClient : CosmosClient
    {
        // The SDK's protected mocking constructor has no client context to dispose.
        protected override void Dispose(bool disposing) { }

        public List<string> Containers { get; } = new();
        public int Creations;
        public CancellationToken Token;
        public Exception Failure { get; } = new InvalidOperationException("Database unavailable");

        public override Container GetContainer(string databaseId, string containerId)
        {
            Containers.Add(databaseId + "/" + containerId);
            return null;
        }

        public override Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(string id, int? throughput = null,
            RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            Creations++;
            Token = cancellationToken;
            Assert.That(id, Is.EqualTo("bdtest-reports"));
            return Task.FromException<DatabaseResponse>(Failure);
        }
    }
}
