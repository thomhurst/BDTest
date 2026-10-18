using System.Net;
using System.Net.Http.Headers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportServerCancellationTests
{
    private static readonly Uri Server = new("https://reports.example/team/reports/");

    [SetUp]
    public void SetUp() => TestResetHelper.ResetData();

    [TearDown]
    public void TearDown() => TestResetHelper.ResetData();

    [TestCase(false)]
    [TestCase(true)]
    public async Task Precancelled_upload_sends_no_requests(bool explicitRun)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var requests = 0;
        using var handler = new Handler((_, _) =>
        {
            requests++;
            return Task.FromResult(Success());
        });
        using var client = new HttpClient(handler);

        await Assert.CatchAsync<OperationCanceledException>(() => Upload(run, explicitRun, client, cancellation.Token));

        Assert.That(requests, Is.Zero);
        using var response = await client.GetAsync(Server);
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Client_owning_overloads_accept_cancellation(bool explicitRun)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => explicitRun
            ? BDTestReportServer.SendDataAndGetReportUriAsync(run, Server, null, cancellation.Token)
            : BDTestReportServer.SendDataAndGetReportUriAsync(Server, null, cancellation.Token));
    }

    [TestCase(false, "ping")]
    [TestCase(true, "ping")]
    [TestCase(false, "data")]
    [TestCase(true, "data")]
    [TestCase(false, "attachment")]
    [TestCase(true, "attachment")]
    public async Task Cancellation_interrupts_request_without_starting_later_requests(bool explicitRun, string phase)
    {
        using var explicitOwner = BDTestRun.Start();
        var run = explicitRun ? explicitOwner : BDTestRun.Default;
        var fixture = new AttachmentFixture(run);
        var scenario = await fixture.When(() => fixture.Attachments.Attach("first.txt", new byte[] { 1 }, "text/plain"))
            .Then(() => fixture.Attachments.Attach("second.txt", new byte[] { 2 }, "text/plain")).BDTestAsync();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<HttpRequestMessage>();
        var recovering = false;
        using var handler = new Handler(async (request, token) =>
        {
            if (recovering) return Success();
            requests.Add(request);
            var path = Required(request.RequestUri).AbsolutePath;
            Assert.That(path, Does.StartWith("/team/reports/"));
            var currentPhase = path.Contains("/attachments/") ? "attachment" : path.Split('/').Last();
            if (currentPhase == phase)
            {
                Assert.That(token.CanBeCanceled, Is.True);
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
            return Success();
        });
        using var client = new HttpClient(handler);
        var upload = Upload(run, explicitRun, client, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await upload.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.That(requests, Has.Count.EqualTo(phase == "ping" ? 1 : phase == "data" ? 2 : 3));
            foreach (var request in requests.Where(request => request.Method == HttpMethod.Post))
            {
                Assert.Throws<ObjectDisposedException>(() => request.Method = HttpMethod.Post);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await Required(request.Content).ReadAsByteArrayAsync());
            }
            // Cancellation releases upload streams, but does not remove captures or dispose the injected client.
            using var capture = run.OpenAttachment(Required(Required(Required(scenario.Steps)[0].Attachments).Single().Id));
            Assert.That(capture.ReadByte(), Is.EqualTo(1));
            recovering = true;
            using var response = await client.GetAsync(Server);
            Assert.That(response.IsSuccessStatusCode, Is.True);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [TestCase(false, "ping")]
    [TestCase(true, "ping")]
    [TestCase(false, "data")]
    [TestCase(true, "data")]
    [TestCase(false, "attachment")]
    [TestCase(true, "attachment")]
    public async Task Cancellation_interrupts_response_body_buffering(bool explicitRun, string phase)
    {
        using var explicitOwner = BDTestRun.Start();
        var run = explicitRun ? explicitOwner : BDTestRun.Default;
        var fixture = new AttachmentFixture(run);
        await fixture.When(() => fixture.Attachments.Attach("first.txt", new byte[] { 1 }, "text/plain"))
            .Then(() => { }).BDTestAsync();
        using var cancellation = new CancellationTokenSource();
        using var content = new BlockingContent();
        var requests = 0;
        using var handler = new Handler((request, _) =>
        {
            requests++;
            var path = Required(request.RequestUri).AbsolutePath;
            return Task.FromResult((phase == "attachment" ? path.Contains("/attachments/") : path.EndsWith("/" + phase))
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                : Success());
        });
        using var client = new HttpClient(handler);
        var upload = Upload(run, explicitRun, client, cancellation.Token);
        try
        {
            await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await upload.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.That(requests, Is.EqualTo(phase == "ping" ? 1 : phase == "data" ? 2 : 3));
            Assert.That(content.Disposed, Is.True);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Response_buffering_timeout_retries_without_cancelling_the_caller(bool explicitRun)
    {
        using var run = BDTestRun.Start();
        using var content = new BlockingContent();
        var attempts = 0;
        using var handler = new Handler((request, _) => Task.FromResult(
            request.Method == HttpMethod.Post && ++attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                : Success()));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };

        var result = await Upload(run, explicitRun, client, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result.AbsoluteUri, Is.EqualTo("https://reports.example/report/server-id"));
        Assert.That(attempts, Is.EqualTo(2));
        Assert.That(content.Disposed, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Cancellation_interrupts_attachment_retry_wait_without_opening_another_capture(bool explicitRun)
    {
        using var explicitOwner = BDTestRun.Start();
        var run = explicitRun ? explicitOwner : BDTestRun.Default;
        var fixture = new AttachmentFixture(run);
        await fixture.When(() => fixture.Attachments.Attach("first.txt", new byte[] { 1 }, "text/plain"))
            .Then(() => fixture.Attachments.Attach("second.txt", new byte[] { 2 }, "text/plain")).BDTestAsync();
        using var cancellation = new CancellationTokenSource();
        var requests = new List<HttpRequestMessage>();
        using var handler = new Handler((request, _) =>
        {
            requests.Add(request);
            if (!Required(request.RequestUri).AbsolutePath.Contains("/attachments/")) return Task.FromResult(Success());
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var upload = Upload(run, explicitRun, client, cancellation.Token);
        Assert.That(requests, Has.Count.EqualTo(3));
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await upload.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(requests, Has.Count.EqualTo(3));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await Required(requests.Last().Content).ReadAsByteArrayAsync());
    }

    [TestCase(false, "delta")]
    [TestCase(true, "delta")]
    [TestCase(false, "date")]
    [TestCase(true, "date")]
    [TestCase(false, "fallback")]
    [TestCase(true, "fallback")]
    public async Task Cancellation_interrupts_retry_delay(bool explicitRun, string retryAfter)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        TrackingContent? content = null;
        using var handler = new Handler((request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(Success());
            attempts++;
            if (retryAfter == "fallback") return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection reset"));
            content = new TrackingContent();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = content };
            response.Headers.RetryAfter = retryAfter == "date"
                ? new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddHours(1))
                : new RetryConditionHeaderValue(TimeSpan.FromHours(1));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        // The handler completes synchronously: the upload returns at the retry delay's await.
        var upload = Upload(run, explicitRun, client, cancellation.Token);
        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(upload.IsCompleted, Is.False);
        if (content != null) Assert.That(content.Disposed, Is.True);
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await upload.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Noncancellation_warmup_failure_remains_best_effort(bool explicitRun)
    {
        using var run = BDTestRun.Start();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        using var handler = new Handler((request, _) =>
        {
            if (request.Method == HttpMethod.Get) throw new HttpRequestException("Warm-up failed");
            attempts++;
            return Task.FromResult(Success());
        });
        using var client = new HttpClient(handler);

        var result = await Upload(run, explicitRun, client, cancellation.Token);

        Assert.That(result.AbsoluteUri, Is.EqualTo("https://reports.example/report/server-id"));
        Assert.That(attempts, Is.EqualTo(1));
    }

    private static Task<Uri> Upload(BDTestRun run, bool explicitRun, HttpClient client, CancellationToken token) => explicitRun
        ? BDTestReportServer.SendDataAndGetReportUriAsync(Server, null, client, run, token)
        : BDTestReportServer.SendDataAndGetReportUriAsync(Server, null, client, token);

    private static HttpResponseMessage Success()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("https://reports.example/report/server-id")
        };
        response.Headers.Add("X-BDTest-Report-Id", new string('a', 32));
        response.Headers.Add("X-BDTest-Attachment-Token", new string('b', 64));
        return response;
    }

    private sealed class AttachmentFixture(BDTestRun run) : AbstractContextBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "upload-cancellation";
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }

        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Model netstandard2.0 content that cannot receive a buffering cancellation token.
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Started.SetResult();
            return completion.Task;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            completion.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent() : StringContent("retry")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
