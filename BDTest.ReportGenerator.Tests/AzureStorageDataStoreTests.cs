using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Xml.Linq;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BDTest.ReportGenerator.RazorServer;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class AzureStorageDataStoreTests
{
    [Test]
    public async Task GetTestDataDisposesDownloadAfterReading()
    {
        using var storage = new StorageHandler(1) { DownloadJson = "{\"id\":\"report\"}" };
        using var client = new HttpClient(storage);

        var result = await CreateStore(client).GetTestData("report", CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo("report"));
        Assert.That(storage.DisposedDownloads, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidJsonDisposesDownload(bool summary)
    {
        using var storage = new StorageHandler(1) { DownloadJson = "{invalid json" };
        using var client = new HttpClient(storage);
        var store = CreateStore(client);

        Assert.ThrowsAsync<JsonReaderException>(async () =>
        {
            if (summary)
                await store.GetAllTestRunRecords();
            else
                await store.GetTestData("report", CancellationToken.None);
        });

        Assert.That(storage.DisposedDownloads, Is.EqualTo(1),
            "Failed deserialization must also close the download.");
    }

    [TestCase(2)]
    [TestCase(51)]
    public async Task GetAllTestRunRecordsDisposesEveryDownload(int availableRecords)
    {
        using var storage = new StorageHandler(availableRecords);
        using var client = new HttpClient(storage);

        var records = (await CreateStore(client).GetAllTestRunRecords()).ToArray();

        Assert.That(records, Has.Length.EqualTo(Math.Min(availableRecords, 50)));
        Assert.That(storage.Downloads, Has.Count.EqualTo(records.Length));
        Assert.That(storage.DisposedDownloads, Is.EqualTo(records.Length),
            "All downloads must be closed before returning, including when the record limit applies.");
    }

    [Test]
    public async Task Lists_newest_fifty_across_pages_in_creation_order_without_deleting()
    {
        using var storage = new StorageHandler(75);
        // Old blobs at the start of the name-ordered listing must not consume the limit.
        storage.CreatedOn[0] = DateTimeOffset.UtcNow.AddDays(-40);
        storage.CreatedOn[1] = DateTimeOffset.UtcNow.AddDays(-35);
        using var client = new HttpClient(storage);
        var store = CreateStore(client);

        var records = (await store.GetAllTestRunRecords()).ToArray();

        Assert.That(records.Select(record => record.RecordId),
            Is.EqualTo(Enumerable.Range(25, 50).Reverse().Select(StorageHandler.Name)));
        Assert.That(storage.Downloads, Has.Count.EqualTo(50));
        Assert.That(storage.ListRequests, Is.EqualTo(3));
        Assert.That(storage.Deletes, Is.Zero);
        Assert.That(storage.DisposedDownloads, Is.EqualTo(50));
    }

    [Test]
    public async Task Reads_do_not_expire_or_delete_old_reports()
    {
        using var storage = new StorageHandler(1);
        storage.CreatedOn[0] = DateTimeOffset.UtcNow.AddDays(-40);
        using var client = new HttpClient(storage);

        var records = await CreateStore(client).GetAllTestRunRecords();

        Assert.That(records.Select(record => record.RecordId), Is.EqualTo(new[] { StorageHandler.Name(0) }));
        Assert.That(storage.Deletes, Is.Zero);
    }

    [Test]
    public async Task Downloads_overlap_but_are_bounded_and_keep_the_sorted_order()
    {
        using var storage = new StorageHandler(60) { GateDownloads = true };
        using var client = new HttpClient(storage);
        var listing = CreateStore(client).GetAllTestRunRecords();
        try
        {
            await storage.FirstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(storage.PeakDownloads, Is.EqualTo(8));
        }
        finally
        {
            storage.ReleaseDownloads.TrySetResult();
            await listing;
        }

        Assert.That(storage.PeakDownloads, Is.InRange(2, 8));
        Assert.That((await listing).Select(record => record.RecordId),
            Is.EqualTo(Enumerable.Range(10, 50).Reverse().Select(StorageHandler.Name)));
    }

    [Test]
    public async Task Empty_container_has_no_downloads()
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        Assert.That(await CreateStore(client).GetAllTestRunRecords(), Is.Empty);
        Assert.That(storage.Downloads, Is.Empty);
    }

    [Test]
    public async Task Timestamp_ties_use_ordinal_names_across_pages()
    {
        using var storage = new StorageHandler(60);
        Array.Fill(storage.CreatedOn, DateTimeOffset.UtcNow);
        using var client = new HttpClient(storage);

        var records = await CreateStore(client).GetAllTestRunRecords();

        Assert.That(records.Select(record => record.RecordId),
            Is.EqualTo(Enumerable.Range(0, 50).Select(StorageHandler.Name)));
    }

    [Test]
    public async Task Omits_a_summary_deleted_after_listing()
    {
        using var storage = new StorageHandler(3) { FailedDownloadStatus = HttpStatusCode.NotFound };
        using var client = new HttpClient(storage);

        var records = await CreateStore(client).GetAllTestRunRecords();

        Assert.That(records.Select(record => record.RecordId),
            Is.EqualTo(new[] { StorageHandler.Name(2), StorageHandler.Name(1) }));
        Assert.That(storage.Deletes, Is.Zero);
    }

    [Test]
    public async Task Propagates_storage_failures_other_than_not_found()
    {
        using var storage = new StorageHandler(1) { FailedDownloadStatus = HttpStatusCode.Forbidden };
        using var client = new HttpClient(storage);

        var exception = await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
            await CreateStore(client).GetAllTestRunRecords());

        Assert.That(exception!.Status, Is.EqualTo(403));
    }

    private static AzureStorageDataStore CreateStore(HttpClient client)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(client) };
        options.Retry.MaxRetries = 0;
        return new AzureStorageDataStore(
            new BlobContainerClient(new Uri("https://storage.test/test-runs-summaries"), options),
            new BlobContainerClient(new Uri("https://storage.test/test-data"), options));
    }

    private sealed class StorageHandler(int count) : HttpMessageHandler
    {
        public DateTimeOffset[] CreatedOn { get; } = Enumerable.Range(0, count)
            .Select(index => DateTimeOffset.UtcNow.AddDays(-2).AddMinutes(index)).ToArray();
        public ConcurrentBag<string> Downloads { get; } = new();
        public int ListRequests;
        public int Deletes;
        public int DisposedDownloads;
        public int PeakDownloads;
        private int _activeDownloads;
        public bool GateDownloads { get; init; }
        public string DownloadJson { get; init; }
        public HttpStatusCode? FailedDownloadStatus { get; init; }
        public TaskCompletionSource FirstBatchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDownloads { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static string Name(int index) => $"00000000-0000-0000-0000-{index:D12}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Delete)
            {
                Interlocked.Increment(ref Deletes);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            if (request.RequestUri!.Query.Contains("comp=list"))
            {
                Interlocked.Increment(ref ListRequests);
                Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query).TryGetValue("marker", out var marker);
                var start = string.IsNullOrEmpty(marker) ? 0 : int.Parse(marker.ToString(), CultureInfo.InvariantCulture);
                var end = Math.Min(start + 25, count);
                var xml = new XElement("EnumerationResults",
                    new XAttribute("ServiceEndpoint", "https://storage.test/"),
                    new XAttribute("ContainerName", "test-runs-summaries"),
                    new XElement("Blobs", Enumerable.Range(start, end - start).Select(index =>
                        new XElement("Blob", new XElement("Name", Name(index)),
                            new XElement("Properties",
                                new XElement("Creation-Time", CreatedOn[index].ToString("R")),
                                new XElement("Last-Modified", CreatedOn[index].ToString("R")),
                                new XElement("Etag", "\"etag\""),
                                new XElement("Content-Length", 100),
                                new XElement("BlobType", "BlockBlob"))))),
                    new XElement("NextMarker", end < count ? end.ToString(CultureInfo.InvariantCulture) : ""));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml.ToString()) };
            }

            var name = request.RequestUri.Segments.Last();
            Downloads.Add(name);
            if (name == Name(0) && FailedDownloadStatus is { } status)
            {
                return new HttpResponseMessage(status) { Content = new StringContent("<Error><Code>StorageError</Code></Error>") };
            }
            var active = Interlocked.Increment(ref _activeDownloads);
            lock (Downloads)
            {
                PeakDownloads = Math.Max(PeakDownloads, active);
            }
            try
            {
                if (GateDownloads)
                {
                    if (active == 8) FirstBatchStarted.TrySetResult();
                    await ReleaseDownloads.Task.WaitAsync(cancellationToken);
                }
                var content = new StreamContent(new TrackedStream(DownloadJson ?? JsonConvert.SerializeObject(new { RecordId = name }),
                    () => Interlocked.Increment(ref DisposedDownloads)));
                content.Headers.LastModified = DateTimeOffset.UtcNow;
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag\"");
                response.Headers.Add("x-ms-blob-type", "BlockBlob");
                return response;
            }
            finally
            {
                Interlocked.Decrement(ref _activeDownloads);
            }
        }
    }

    private sealed class TrackedStream(string content, Action onDispose) : MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
    {
        private int _disposed;
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
            base.Dispose(disposing);
        }
    }
}
