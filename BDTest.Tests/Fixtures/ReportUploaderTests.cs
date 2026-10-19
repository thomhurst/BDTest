using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using BDTest.Helpers;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class ReportUploaderTests
{
    private static readonly Uri Server = new("https://reports.example/team/reports/");
    private static readonly Uri ReportUri = new("https://reports.example/team/reports/report/result");

    [Test]
    [Arguments("relative")]
    [Arguments("ftp://reports.example")]
    [Arguments("https://reports.example/?secret=x")]
    [Arguments("https://reports.example/#fragment")]
    [Arguments("https://user:password@reports.example")]
    public async Task Rejects_invalid_addresses(string address)
        => await Assert.That(() => new BDTestReportUploadOptions(new Uri(address, UriKind.RelativeOrAbsolute))).ThrowsExactly<ArgumentException>();

    [Test]
    [Arguments(0)]
    [Arguments(-2)]
    [Arguments(2147483648)]
    public async Task Rejects_invalid_timeouts(long milliseconds)
    {
        using var client = new HttpClient();
        await Assert.That(() => new BDTestReportUploader(client, new(Server) { Timeout = TimeSpan.FromMilliseconds(milliseconds) }))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("secret\r\nInjected: value")]
    [Arguments("secret\0")]
    public async Task Rejects_invalid_keys(string key)
    {
        using var client = new HttpClient();
        await Assert.That(() => new BDTestReportUploader(client, new(Server) { UploadKey = key })).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Rejects_null_arguments()
    {
        using var client = new HttpClient();
        await Assert.That(() => new BDTestReportUploadOptions(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new BDTestReportUploader(null!, new(Server))).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new BDTestReportUploader(client, null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Environment_configuration_is_explicit_and_validated()
    {
        var variable = "BDTEST_UPLOAD_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            await Assert.That(() => BDTestReportUploadOptions.FromEnvironment(Server, variable)).ThrowsExactly<InvalidOperationException>();
            Environment.SetEnvironmentVariable(variable, " ");
            await Assert.That(() => BDTestReportUploadOptions.FromEnvironment(Server, variable)).ThrowsExactly<ArgumentException>();
            Environment.SetEnvironmentVariable(variable, "environment-secret");
            var options = BDTestReportUploadOptions.FromEnvironment(Server, variable);
            using var handler = new Handler(async (request, _) =>
            {
                await Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key").Single()).IsEqualTo("environment-secret");
                return Success();
            });
            using var client = new HttpClient(handler);
            using var run = BDTestRun.Start();
            await new BDTestReportUploader(client, options).UploadAsync(run);
            await Assert.That(new BDTestReportUploadOptions(Server).UploadKey).IsNull();
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Test]
    [Arguments(true, "https://reports.example", "/bdtest/data")]
    [Arguments(false, "https://reports.example/team/reports", "/team/reports/data")]
    [Arguments(true, "https://reports.example/team/reports/", "/team/reports/data")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "Intentionally leave a definition pending to verify the upload snapshot.")]
    public async Task Streams_snapshot_and_recreates_content_on_retry(bool gzip, string address, string expectedPath)
    {
        using var run = BDTestRun.Start();
        using var other = BDTestRun.Start();
        var fixture = new Capture(run);
        await fixture.When(() => { }).Then(() => { }).WithScenarioText("owned").BDTestAsync();
        _ = fixture.When(() => { }).Then(() => { }).WithScenarioText("pending");
        await new Capture(other).When(() => { }).Then(() => { }).WithScenarioText("other").BDTestAsync();
        var expected = BDTestJsonHelper.Serialize(BDTestOutputModelBuilder.Build(run, null));
        var contents = new List<HttpContent>();
        var requests = new List<HttpRequestMessage>();
        using var handler = new Handler(async (request, _) =>
        {
            requests.Add(request);
            await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo(expectedPath);
            await Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key").Single()).IsEqualTo("secret");
            var content = request.Content!;
            contents.Add(content);
            await Assert.That(content.Headers.ContentLength).IsNull();
            await Assert.That(content.Headers.ContentType!.MediaType).IsEqualTo("application/json");
            await Assert.That(content.Headers.ContentEncoding.Contains("gzip")).IsEqualTo(gzip);
            using var destination = new MemoryStream();
            await content.CopyToAsync(destination);
            destination.Position = 0;
            using var decompressed = gzip ? new GZipStream(destination, CompressionMode.Decompress, leaveOpen: true) : null;
            using var reader = new StreamReader((Stream?)decompressed ?? destination);
            await Assert.That(await reader.ReadToEndAsync()).IsEqualTo(expected);
            return contents.Count == 1 ? new(HttpStatusCode.ServiceUnavailable) : Success();
        });
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(23), BaseAddress = new Uri("https://unrelated.example") };
        client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", "default-secret");
        var options = new BDTestReportUploadOptions(new(address)) { UseGzip = gzip, UploadKey = "secret" };
        var uploader = new BDTestReportUploader(client, options);
        options.UploadKey = "changed-after-construction";
        options.UseGzip = !gzip;
        await Assert.That(await uploader.UploadAsync(run)).IsEqualTo(ReportUri);
        await Assert.That(contents.Count).IsEqualTo(2);
        await Assert.That(ReferenceEquals(contents[0], contents[1])).IsFalse();
        foreach (var request in requests)
            await Assert.That(() => request.Method = HttpMethod.Get).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(client.Timeout).IsEqualTo(TimeSpan.FromSeconds(23));
        await Assert.That(client.BaseAddress).IsEqualTo(new Uri("https://unrelated.example"));
        await Assert.That(client.DefaultRequestHeaders.GetValues("X-BDTest-Upload-Key").Single()).IsEqualTo("default-secret");
        // A second upload proves the caller still owns a usable client.
        await uploader.UploadAsync(run);
    }

    [Test]
    public async Task Default_gzip_and_empty_run_timing_and_disposed_validation()
    {
        using var run = BDTestRun.Start();
        var requests = 0;
        using var handler = new Handler(async (request, _) =>
        {
            requests++;
            await Assert.That(request.Content!.Headers.ContentEncoding.Single()).IsEqualTo("gzip");
            using var bytes = new MemoryStream(await request.Content.ReadAsByteArrayAsync());
            using var gzip = new GZipStream(bytes, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var report = BDTestJsonHelper.DeserializeReport(await reader.ReadToEndAsync());
            await Assert.That(report.Id).IsEqualTo(run.ReportId);
            await Assert.That(report.Scenarios).IsEmpty();
            await Assert.That(report.TestTimer!.TestsStartedAt).IsEqualTo(new DateTimeOffset(run.StartedAt));
            await Assert.That(report.TestTimer.TestsFinishedAt).IsGreaterThanOrEqualTo(report.TestTimer.TestsStartedAt);
            return Success();
        });
        using var client = new HttpClient(handler);
        var uploader = new BDTestReportUploader(client, new(Server));
        await uploader.UploadAsync(run);
        run.Dispose();
        await Assert.That(async () => await uploader.UploadAsync(run)).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(requests).IsEqualTo(1);
    }

    [Test]
    [Arguments(HttpStatusCode.BadRequest, 1)]
    [Arguments(HttpStatusCode.Unauthorized, 1)]
    [Arguments(HttpStatusCode.RequestEntityTooLarge, 1)]
    [Arguments(HttpStatusCode.NotImplemented, 1)]
    [Arguments(HttpStatusCode.RequestTimeout, 3)]
    [Arguments(HttpStatusCode.TooManyRequests, 3)]
    [Arguments(HttpStatusCode.InternalServerError, 3)]
    [Arguments(HttpStatusCode.BadGateway, 3)]
    [Arguments(HttpStatusCode.ServiceUnavailable, 3)]
    [Arguments(HttpStatusCode.GatewayTimeout, 3)]
    public async Task Retries_only_transient_statuses(HttpStatusCode status, int expected)
    {
        using var run = BDTestRun.Start();
        var attempts = 0;
        using var handler = new Handler((_, _) => { attempts++; return Task.FromResult(new HttpResponseMessage(status)); });
        using var client = new HttpClient(handler);
        await Assert.That(async () => await new BDTestReportUploader(client, new(Server)).UploadAsync(run)).ThrowsExactly<HttpRequestException>();
        await Assert.That(attempts).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Honours_retry_after(bool date)
    {
        using var run = BDTestRun.Start();
        var attempts = 0;
        var elapsed = new Stopwatch();
        var retryAt = DateTimeOffset.UtcNow;
        using var handler = new Handler(async (_, _) =>
        {
            if (++attempts > 1)
            {
                if (date) await Assert.That(DateTimeOffset.UtcNow).IsGreaterThanOrEqualTo(retryAt);
                else await Assert.That(elapsed.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
                return Success();
            }
            retryAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2);
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = date ? new RetryConditionHeaderValue(retryAt) : new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            elapsed.Start();
            return response;
        });
        using var client = new HttpClient(handler);
        await new BDTestReportUploader(client, new(Server)).UploadAsync(run);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    [Arguments("request", false)]
    [Arguments("response", false)]
    [Arguments("request", true)]
    [Arguments("response", true)]
    [Arguments("backoff", true)]
    public async Task Timeout_retries_but_caller_cancellation_stops(string phase, bool cancel)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        using var blockedBody = new BlockingContent();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        using var handler = new Handler(async (_, token) =>
        {
            if (++attempts > 1) return Success();
            if (phase == "response") return new(HttpStatusCode.OK) { Content = blockedBody };
            started.TrySetResult();
            if (phase == "request") await Task.Delay(Timeout.Infinite, token);
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
            return response;
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var uploader = new BDTestReportUploader(client, new(Server) { Timeout = cancel ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100) });
        var upload = uploader.UploadAsync(run, cancellationToken: cancellation.Token);
        try
        {
            await (phase == "response" ? blockedBody.Started.Task : started.Task).WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.That(async () => await upload.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
                await Assert.That(attempts).IsEqualTo(1);
            }
            else
            {
                await Assert.That(await upload.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(ReportUri);
                await Assert.That(attempts).IsEqualTo(2);
            }
            if (phase == "response") await Assert.That(blockedBody.Disposed).IsTrue();
        }
        finally { cancellation.Cancel(); }
    }

    [Test]
    public async Task Precancelled_token_sends_nothing()
    {
        using var run = BDTestRun.Start();
        using var handler = new Handler((_, _) => throw new InvalidOperationException("Must not send"));
        using var client = new HttpClient(handler);
        await Assert.That(async () => await new BDTestReportUploader(client, new(Server)).UploadAsync(run, cancellationToken: new CancellationToken(true)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Serialization_observes_attempt_timeout_and_caller_cancellation(bool gzip, bool cancel)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        using var blocked = new BlockingWriteStream();
        var attempts = 0;
        using var handler = new Handler(async (request, _) =>
        {
            if (++attempts == 1)
            {
                // Deliberately use the overload without a token, like netstandard2.0 handlers.
                await request.Content!.CopyToAsync(blocked);
            }
            return Success();
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var uploader = new BDTestReportUploader(client, new(Server)
        {
            UseGzip = gzip,
            Timeout = cancel ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100)
        });
        var upload = uploader.UploadAsync(run, cancellationToken: cancellation.Token);
        try
        {
            if (cancel)
            {
                await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancellation.Cancel();
                await Assert.That(async () => await upload.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
                await Assert.That(attempts).IsEqualTo(1);
            }
            else
            {
                await Assert.That(await upload.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(ReportUri);
                await Assert.That(attempts).IsEqualTo(2);
            }
        }
        finally { cancellation.Cancel(); }
    }

    [Test]
    public async Task Attachments_follow_report_and_reopen_run_owned_streams_on_retry()
    {
        using var run = BDTestRun.Start();
        using var other = BDTestRun.Start();
        var fixture = new Capture(run);
        var scenario = await fixture.When(() => fixture.Attachments.Attach("owned.txt", new byte[] { 1, 2 }, "text/plain")).Then(() => { }).BDTestAsync();
        var otherFixture = new Capture(other);
        await otherFixture.When(() => otherFixture.Attachments.Attach("other.txt", new byte[] { 3 }, "text/plain")).Then(() => { }).BDTestAsync();
        var id = scenario.Steps[0].Attachments.Single().Id;
        var attempts = 0;
        var contents = new List<HttpContent>();
        using var handler = new Handler(async (request, _) =>
        {
            await Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key").Single()).IsEqualTo("key");
            if (++attempts == 1)
            {
                await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo("/team/reports/data");
                return Success();
            }
            await Assert.That(request.RequestUri!.AbsolutePath).IsEqualTo($"/team/reports/data/{new string('a', 32)}/attachments/{id}");
            await Assert.That(request.Headers.GetValues("X-BDTest-Attachment-Token").Single()).IsEqualTo("receipt");
            await Assert.That(request.Content!.Headers.ContentEncoding).IsEmpty();
            await Assert.That(request.Content.Headers.ContentLength).IsEqualTo(2L);
            contents.Add(request.Content);
            await Assert.That(await request.Content.ReadAsByteArrayAsync()).IsEquivalentTo(new byte[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            return attempts == 2 ? new(HttpStatusCode.ServiceUnavailable) : Success();
        });
        using var client = new HttpClient(handler);
        await new BDTestReportUploader(client, new(Server) { UploadKey = "key" }).UploadAsync(run);
        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(ReferenceEquals(contents[0], contents[1])).IsFalse();
        using var retained = run.OpenAttachment(id);
        await Assert.That(retained.Length).IsEqualTo(2L);
    }

    private static HttpResponseMessage Success()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReportUri.AbsoluteUri) };
        response.Headers.Add("X-BDTest-Report-Id", new string('a', 32));
        response.Headers.Add("X-BDTest-Attachment-Token", "receipt");
        return response;
    }

    private sealed class Capture(BDTestRun run) : AbstractContextBDTestBase<object>
    {
        private readonly string id = Guid.NewGuid().ToString("N");
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => id;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Started.TrySetResult();
            return completion.Task;
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            completion.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingWriteStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => new(WriteAsync(Array.Empty<byte>(), 0, 0, cancellationToken));
    }
}
