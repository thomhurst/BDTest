using System.IO.Compression;
using System.Net;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class StreamingUploadTests
{
    [Test]
    public async Task Gzip_finishes_with_async_writes_and_leaves_destination_open()
    {
        using var run = BDTestRun.Start();
        using var destination = new AsyncOnlyStream(false);
        using var handler = new UploadHandler(destination);
        using var client = new HttpClient(handler);
        await new BDTestReportUploader(client, new(new Uri("https://reports.example"))).UploadAsync(run);

        Assert.That(destination.CanWrite, Is.True);
        Assert.That(destination.FinalWriteStarted.Task.IsCompletedSuccessfully, Is.True);
        destination.Position = 0;
        using var gzip = new GZipStream(destination, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new StreamReader(gzip);
        using var report = System.Text.Json.JsonDocument.Parse(await reader.ReadToEndAsync());
        Assert.That(report.RootElement.GetProperty("id").GetString(), Is.EqualTo(run.ReportId));
        // Reading to EOF validates that the final compressed payload and trailer were sent.
        Assert.That(destination.Length, Is.GreaterThan(18));
    }

    [Test]
    public async Task Cancellation_interrupts_the_final_gzip_write()
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        using var destination = new AsyncOnlyStream(true);
        using var handler = new UploadHandler(destination);
        using var client = new HttpClient(handler);
        var uploader = new BDTestReportUploader(client, new(new Uri("https://reports.example"))
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        var upload = uploader.UploadAsync(run, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(destination.FinalWriteStarted.Task, upload).WaitAsync(TimeSpan.FromSeconds(10));
            if (first == upload) await upload; // Surface an unexpected synchronous-write failure immediately.
            cancellation.Cancel();
            await Assert.ThatAsync(async () => await upload.WaitAsync(TimeSpan.FromSeconds(10)), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(handler.Attempts, Is.EqualTo(1));
            Assert.That(destination.CanWrite, Is.True);
        }
        finally { cancellation.Cancel(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Typed_and_legacy_receipts_work_on_each_library_asset(bool typed)
    {
        using var run = BDTestRun.Start();
        using var handler = new ReceiptHandler(typed);
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
        var result = await new BDTestReportUploader(client, new(new Uri("https://reports.example"))).UploadReportAsync(run);
        Assert.That(result.ReportUrl.AbsoluteUri, Is.EqualTo("https://reports.example/result"));
        Assert.That(result.ReportId, Is.EqualTo(new string('a', 32)));
        Assert.That(client.DefaultRequestHeaders.Accept.Single().MediaType, Is.EqualTo("application/xml"));
    }

    private sealed class ReceiptHandler(bool typed) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.Headers.GetValues("X-BDTest-Upload-Protocol").Single(), Is.EqualTo("1"));
            Assert.That(request.Headers.Accept.Single().MediaType, Is.EqualTo("application/json"));
            var response = new HttpResponseMessage(typed ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = new StringContent(typed
                    ? "{\"reportUrl\":\"https://reports.example/result\",\"reportId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}"
                    : "https://reports.example/result")
            };
            response.Headers.Add("X-BDTest-Report-Id", new string('a', 32));
            return Task.FromResult(response);
        }
    }
    private sealed class UploadHandler(Stream destination) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            await request.Content!.CopyToAsync(destination);
            return new(HttpStatusCode.OK) { Content = new StringContent("https://reports.example/report/result") };
        }
    }

    private sealed class AsyncOnlyStream(bool blockFinalWrite) : MemoryStream
    {
        public TaskCompletionSource FinalWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("Network writes must be asynchronous, including gzip finalization.");

        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new InvalidOperationException("Network writes must be asynchronous, including gzip finalization.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteBytesAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => new(WriteBytesAsync(buffer, cancellationToken));

        private async Task WriteBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            // An empty report fits in gzip's buffer: the header is emitted first,
            // then the compressed body and trailer are emitted at finalization.
            if (buffer.Length > 10)
            {
                FinalWriteStarted.TrySetResult();
                if (blockFinalWrite) await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = buffer.ToArray();
            base.Write(bytes, 0, bytes.Length);
        }
    }
}
