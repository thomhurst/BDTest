using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Xml.Linq;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BDTest.ReportGenerator.RazorServer;
using BDTest.Razor.Reports.Storage;
using BDTest.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class AzureStorageDataStoreTests
{
    [Test]
    public async Task Attachment_write_and_read_stream_owned_bytes_with_report_scoped_keys()
    {
        var report = Guid.NewGuid().ToString("N");
        var attachment = Guid.NewGuid().ToString("N");
        using var storage = new StorageHandler(0) { DownloadJson = "content" };
        using var client = new HttpClient(storage);
        var store = CreateStore(client);
        using var input = new NonSeekableStream("content", () => { });
        await store.WriteAsync(report, attachment, input, 7);
        Assert.That(input.CanRead, Is.True);
        Assert.That(storage.Uploads[$"/test-data/{report}/attachments/{attachment}"], Is.EqualTo("content"));
        using (var stream = await store.OpenReadAsync(report, attachment))
        using (var reader = new StreamReader(stream ?? throw new AssertionException("Expected stored content.")))
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("content"));
        Assert.That(storage.DisposedDownloads, Is.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(3)]
    public async Task Invalid_attachment_length_never_uploads_partial_blob(int length)
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        using var input = new NonSeekableStream("ab", () => { });
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore(client).WriteAsync(
            Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), input, length));
        Assert.That(storage.Requests, Is.Zero);
        Assert.That(input.CanRead, Is.True);
    }

    [Test]
    public async Task Attachment_deletion_lists_only_exact_report_prefix_across_pages()
    {
        var report = Guid.NewGuid().ToString("N");
        using var storage = new StorageHandler(27) { AttachmentReport = report };
        using var client = new HttpClient(storage);
        await CreateStore(client).DeleteReportAsync(report);
        Assert.That(storage.ListRequests, Is.EqualTo(2));
        Assert.That(storage.DeletedPaths, Has.Count.EqualTo(27));
        Assert.That(storage.DeletedPaths.All(path => path.StartsWith($"/test-data/{report}/attachments/", StringComparison.Ordinal)), Is.True);
    }

    [TestCase("read")]
    [TestCase("write")]
    [TestCase("delete")]
    public async Task Attachment_operations_observe_cancellation_without_storage_requests(string operation)
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new MemoryStream(new byte[] { 1 });
        var report = Guid.NewGuid().ToString("N");
        var attachment = Guid.NewGuid().ToString("N");
        var store = CreateStore(client);
        await Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            if (operation == "read") using (await store.OpenReadAsync(report, attachment, cancellation.Token)) { }
            if (operation == "write") await store.WriteAsync(report, attachment, input, 1, cancellation.Token);
            if (operation == "delete") await store.DeleteReportAsync(report, cancellation.Token);
        });
        Assert.That(storage.Requests, Is.Zero);
        Assert.That(input.CanRead, Is.True);
    }

    [TestCase("read")]
    [TestCase("write")]
    [TestCase("delete report")]
    [TestCase("list")]
    [TestCase("upsert")]
    [TestCase("delete index")]
    public async Task Streaming_operations_observe_cancellation_before_start(string operation)
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new MemoryStream("{}"u8.ToArray());
        var store = CreateStore(client);

        await Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "read":
                    using (await store.OpenReadAsync("report", cancellation.Token)) { }
                    break;
                case "write":
                    await store.WriteAsync("report", input, cancellation.Token);
                    break;
                case "delete report":
                    await ((IReportStore)store).DeleteAsync("report", cancellation.Token);
                    break;
                case "list":
                    await store.ListAsync(new RunIndexQuery(), cancellation.Token);
                    break;
                case "upsert":
                    await store.UpsertAsync(new RunIndexEntry { RecordId = "report" }, cancellation.Token);
                    break;
                case "delete index":
                    await ((IRunIndexStore)store).DeleteAsync("report", cancellation.Token);
                    break;
            }
        });

        Assert.That(storage.Requests, Is.Zero);
        Assert.That(input.CanRead, Is.True);
    }

    [Test]
    public async Task Report_stream_is_raw_and_owned_by_caller()
    {
        const string json = "{\"unknown-report-shape\":[1,2,3]}";
        using var storage = new StorageHandler(1) { DownloadJson = json };
        using var client = new HttpClient(storage);
        var stream = await CreateStore(client).OpenReadAsync("report");

        Assert.That(storage.DisposedDownloads, Is.Zero);
        using (stream)
        using (var reader = new StreamReader(stream ?? throw new AssertionException("Expected stored content.")))
        {
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(json));
        }
        Assert.That(storage.DisposedDownloads, Is.EqualTo(1));
    }

    [Test]
    public async Task Missing_report_stream_returns_null()
    {
        using var storage = new StorageHandler(1) { FailedDownloadStatus = HttpStatusCode.NotFound };
        using var client = new HttpClient(storage);

        Assert.That(await CreateStore(client).OpenReadAsync(StorageHandler.Name(0)), Is.Null);
    }

    [Test]
    public async Task Report_write_copies_raw_nonseekable_stream_without_disposing_input()
    {
        const string json = "{\"custom\":\"report payload\"}";
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        var disposed = false;
        using var input = new NonSeekableStream(json, () => disposed = true);

        await CreateStore(client).WriteAsync("report", input);

        Assert.That(storage.Uploads["/test-data/report"], Is.EqualTo(json));
        Assert.That(disposed, Is.False);
    }

    [TestCase(true, "/test-data/report")]
    [TestCase(false, "/test-runs-summaries/report")]
    public async Task Streaming_delete_targets_only_its_container(bool report, string expectedPath)
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        var store = CreateStore(client);

        var deleted = report
            ? await ((IReportStore)store).DeleteAsync("report", CancellationToken.None)
            : await ((IRunIndexStore)store).DeleteAsync("report", CancellationToken.None);

        Assert.That(deleted, Is.True);
        Assert.That(storage.DeletedPaths, Is.EqualTo(new[] { expectedPath }));
    }

    [Test]
    public async Task Index_reads_every_page_and_pages_filtered_entries_by_run_timestamp()
    {
        var started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var storage = new StorageHandler(75)
        {
            SummaryJson = index => ReportJson.SerializeObject(new
            {
                RecordId = StorageHandler.Name(index),
                // Run order deliberately opposes blob creation order.
                StartedAtDateTime = started.AddMinutes(-index),
                BranchName = index % 2 == 0 ? "main" : "other",
                Environment = "CI",
                Tag = "release",
                Counts = new { Total = index, Passed = index }
            })
        };
        using var client = new HttpClient(storage);
        var store = CreateStore(client);
        var first = await store.ListAsync(new RunIndexQuery
        {
            PageSize = 30, BranchName = "main", Environment = "CI", Tag = "release"
        });
        var second = await store.ListAsync(new RunIndexQuery
        {
            PageSize = 30, BranchName = "main", Environment = "CI", Tag = "release",
            ContinuationToken = first.ContinuationToken
        });

        Assert.That(first.Entries.Select(entry => entry.RecordId),
            Is.EqualTo(Enumerable.Range(0, 30).Select(index => StorageHandler.Name(index * 2))));
        Assert.That(second.Entries.Select(entry => entry.RecordId),
            Is.EqualTo(Enumerable.Range(30, 8).Select(index => StorageHandler.Name(index * 2))));
        Assert.That(first.Entries[1].Counts?.Total, Is.EqualTo(2));
        Assert.That(first.ContinuationToken, Is.Not.Null.And.Not.Empty);
        Assert.That(second.ContinuationToken, Is.Null);
        Assert.That(storage.ListRequests, Is.EqualTo(6));
        Assert.That(storage.Downloads, Has.Count.EqualTo(150));
        Assert.That(storage.DisposedDownloads, Is.EqualTo(150));
        Assert.That(storage.Deletes, Is.Zero);
    }

    [Test]
    public async Task Index_can_return_more_than_fifty_entries()
    {
        using var storage = new StorageHandler(75);
        using var client = new HttpClient(storage);

        var page = await CreateStore(client).ListAsync(new RunIndexQuery { PageSize = 100 });

        Assert.That(page.Entries, Has.Count.EqualTo(75));
        Assert.That(page.ContinuationToken, Is.Null);
    }

    [Test]
    public async Task Index_downloads_are_bounded_and_observe_cancellation()
    {
        using var storage = new StorageHandler(75) { GateDownloads = true };
        using var client = new HttpClient(storage);
        using var cancellation = new CancellationTokenSource();
        var listing = CreateStore(client).ListAsync(new RunIndexQuery(), cancellation.Token);
        try
        {
            await storage.FirstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(storage.PeakDownloads, Is.EqualTo(8));
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await listing);
        }
        finally
        {
            storage.ReleaseDownloads.TrySetResult();
        }
        Assert.That(storage.ListRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task Index_upsert_writes_compatible_summary_json()
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        var entry = new RunIndexEntry
        {
            RecordId = "report", BranchName = "main", Counts = new RunIndexCounts { Total = 3, Passed = 2 }
        };

        await CreateStore(client).UpsertAsync(entry);

        var json = System.Text.Json.JsonDocument.Parse(storage.Uploads["/test-runs-summaries/report"]);
        using (json)
        {
            Assert.That(json.RootElement.GetProperty("RecordId").GetString(), Is.EqualTo("report"));
            Assert.That(json.RootElement.GetProperty("Counts").GetProperty("Passed").GetInt32(), Is.EqualTo(2));
        }
    }

    [TestCase(true, "/test-data/report-123")]
    [TestCase(false, "/test-runs-summaries/report-123")]
    public async Task DeleteTargetsOnlyItsOwnContainer(bool deleteTestData, string expectedPath)
    {
        using var storage = new StorageHandler(0);
        using var client = new HttpClient(storage);
        var store = CreateStore(client);

        if (deleteTestData)
        {
            await store.DeleteTestData("report-123");
        }
        else
        {
            await store.DeleteTestRunRecord("report-123");
        }

        Assert.That(storage.DeletedPaths, Is.EqualTo(new[] { expectedPath }));
    }

    [Test]
    public async Task GetTestDataDisposesDownloadAfterReading()
    {
        using var storage = new StorageHandler(1) { DownloadJson = "{\"id\":\"report\"}" };
        using var client = new HttpClient(storage);

        var result = await CreateStore(client).GetTestData("report", CancellationToken.None);

        Assert.That(result?.Id, Is.EqualTo("report"));
        Assert.That(storage.DisposedDownloads, Is.EqualTo(1));
    }

    [Test]
    public async Task HistoricalReportLoadsThroughStorageAndSurvivesCurrentStorageSerialization()
    {
        using var storage = new StorageHandler(1) { DownloadJson = HistoricalReportFixture.ReadJson() };
        using var client = new HttpClient(storage);
        var report = await CreateStore(client).GetTestData("1113e3bf269c489187245c4339dfd946", CancellationToken.None);
        HistoricalReportFixture.AssertCompatible(report ?? throw new AssertionException("Expected historical report."));

        // StoreTestData uses AsStream; reload its JSON through the production download reader.
        using var stream = report.AsStream();
        using var reader = new StreamReader(stream);
        using var restoredStorage = new StorageHandler(1) { DownloadJson = await reader.ReadToEndAsync() };
        using var restoredClient = new HttpClient(restoredStorage);
        var restored = await CreateStore(restoredClient).GetTestData(report.Id ?? throw new AssertionException("Expected historical report ID."), CancellationToken.None);
        HistoricalReportFixture.AssertCompatible(restored ?? throw new AssertionException("Expected restored historical report."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InvalidJsonDisposesDownload(bool summary)
    {
        using var storage = new StorageHandler(1) { DownloadJson = "{invalid json" };
        using var client = new HttpClient(storage);
        var store = CreateStore(client);

        await Assert.CatchAsync<System.Text.Json.JsonException>(async () =>
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
        public ConcurrentBag<string> DeletedPaths { get; } = new();
        public ConcurrentDictionary<string, string> Uploads { get; } = new();
        public string? AttachmentReport { get; init; }
        public int ListRequests;
        public int Requests;
        public int Deletes;
        public int DisposedDownloads;
        public int PeakDownloads;
        private int _activeDownloads;
        public bool GateDownloads { get; init; }
        public string? DownloadJson { get; init; }
        public Func<int, string>? SummaryJson { get; init; }
        public HttpStatusCode? FailedDownloadStatus { get; init; }
        public TaskCompletionSource FirstBatchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDownloads { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static string Name(int index) => $"00000000-0000-0000-0000-{index:D12}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            if (request.Method == HttpMethod.Put)
            {
                if (!request.RequestUri!.Query.Contains("comp=blocklist"))
                    Uploads[request.RequestUri.AbsolutePath] = await request.Content!.ReadAsStringAsync(cancellationToken);
                var uploaded = new HttpResponseMessage(HttpStatusCode.Created);
                uploaded.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag\"");
                uploaded.Headers.Add("x-ms-request-server-encrypted", "true");
                return uploaded;
            }
            if (request.Method == HttpMethod.Delete)
            {
                DeletedPaths.Add(request.RequestUri!.AbsolutePath);
                Interlocked.Increment(ref Deletes);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            if (request.RequestUri!.Query.Contains("comp=list"))
            {
                Interlocked.Increment(ref ListRequests);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
                query.TryGetValue("marker", out var marker);
                if (AttachmentReport != null)
                    Assert.That(query["prefix"].ToString(), Is.EqualTo($"{AttachmentReport}/attachments/"));
                var start = string.IsNullOrEmpty(marker) ? 0 : int.Parse(marker.ToString(), CultureInfo.InvariantCulture);
                var end = Math.Min(start + 25, count);
                var xml = new XElement("EnumerationResults",
                    new XAttribute("ServiceEndpoint", "https://storage.test/"),
                    new XAttribute("ContainerName", "test-runs-summaries"),
                    new XElement("Blobs", Enumerable.Range(start, end - start).Select(index =>
                        new XElement("Blob", new XElement("Name", AttachmentReport == null ? Name(index) : $"{AttachmentReport}/attachments/{index:x32}"),
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
                var content = new StreamContent(new TrackedStream(DownloadJson ?? SummaryJson?.Invoke(int.Parse(name[^12..], CultureInfo.InvariantCulture)) ?? ReportJson.SerializeObject(new { RecordId = name }),
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

    private sealed class NonSeekableStream(string content, Action onDispose) : Stream
    {
        private readonly MemoryStream _inner = new(System.Text.Encoding.UTF8.GetBytes(content));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                onDispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
