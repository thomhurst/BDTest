using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using BDTest.Settings;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportServerRetryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RateLimitedUploadWaitsForRetryAfter(bool useDate)
    {
        DateTimeOffset retryAt = default;
        var elapsed = new Stopwatch();
        var requests = new List<string>();
        using var handler = new UploadHandler(async (request, attempt) =>
        {
            requests.Add(await request.Content.ReadAsStringAsync());
            Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key"), Is.EqualTo(new[] { "secret" }));
            if (attempt == 1)
            {
                elapsed.Restart();
                retryAt = useDate
                    ? DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2)
                    : DateTimeOffset.UtcNow.AddSeconds(1);
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = useDate
                    ? new RetryConditionHeaderValue(retryAt)
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return response;
            }

            if (useDate)
            {
                Assert.That(DateTimeOffset.UtcNow, Is.GreaterThanOrEqualTo(retryAt));
            }
            else
            {
                // Relative delays must use a monotonic clock; UTC can be adjusted while waiting.
                Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1)));
            }
            return Success();
        });
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", "secret");

        var result = await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);

        Assert.That(result, Is.EqualTo(new Uri("https://reports.example/bdtest/report/server-id")));
        Assert.That(handler.UploadAttempts, Is.EqualTo(2));
        Assert.That(requests[1], Is.EqualTo(requests[0]));
    }

    [TestCase(null)]
    [TestCase("invalid")]
    [TestCase("0")]
    [TestCase("Wed, 21 Oct 2015 07:28:00 GMT")]
    public async Task MissingInvalidOrExpiredRetryAfterDoesNotPreventRecovery(string header)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler((_, attempt) =>
        {
            if (attempt > 1)
            {
                Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)));
                return Task.FromResult(Success());
            }
            elapsed.Start();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (header != null) response.Headers.TryAddWithoutValidation("Retry-After", header);
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);

        Assert.That(handler.UploadAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task PersistentRateLimitStopsAfterThreeAttemptsWithoutWaitingAfterTheLast()
    {
        using var handler = new UploadHandler((_, attempt) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(attempt == 3 ? TimeSpan.FromHours(1) : TimeSpan.Zero);
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(handler.UploadAttempts, Is.EqualTo(3));
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("https://reports.example/bdtest/report/server-id")
    };

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.Conflict)]
    [TestCase(HttpStatusCode.RequestEntityTooLarge)]
    [TestCase(HttpStatusCode.NotImplemented)]
    [TestCase(HttpStatusCode.HttpVersionNotSupported)]
    public async Task PermanentFailuresAreNotRetried(HttpStatusCode status)
    {
        using var handler = new UploadHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client));

        Assert.That(handler.UploadAttempts, Is.EqualTo(1));
    }

    [TestCase(HttpStatusCode.RequestTimeout)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public async Task TransientFailuresBackOffAndRecover(HttpStatusCode status)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler((_, attempt) =>
        {
            if (attempt > 1)
            {
                Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250 * (attempt - 1))));
            }
            elapsed.Restart();
            return Task.FromResult(attempt < 3 ? new HttpResponseMessage(status) : Success());
        });
        using var client = new HttpClient(handler);

        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);

        Assert.That(handler.UploadAttempts, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TransportFailuresAndTimeoutsCanRecover(bool timeout)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler((_, attempt) =>
        {
            if (attempt > 1)
            {
                Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)));
                return Task.FromResult(Success());
            }
            elapsed.Start();
            return Task.FromException<HttpResponseMessage>(timeout ? new TaskCanceledException("Timeout") : new HttpRequestException("Connection reset"));
        });
        using var client = new HttpClient(handler);

        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);

        Assert.That(handler.UploadAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task ProgrammingErrorsAreNotRetried()
    {
        using var handler = new UploadHandler((_, _) => throw new InvalidOperationException("Invalid handler configuration"));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client));

        Assert.That(handler.UploadAttempts, Is.EqualTo(1));
    }

    [TestCase(HttpStatusCode.OK, 1)]
    [TestCase(HttpStatusCode.BadRequest, 1)]
    [TestCase(HttpStatusCode.ServiceUnavailable, 3)]
    public async Task ResponsesAndRequestsAreDisposedButInjectedClientRemainsUsable(HttpStatusCode status, int attempts)
    {
        using var handler = new TrackingHandler(status);
        using var client = new HttpClient(handler);
        var upload = BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);
        if (status == HttpStatusCode.OK) await upload;
        else await Assert.ThrowsAsync<HttpRequestException>(async () => await upload);

        Assert.That(handler.Contents, Has.Count.EqualTo(attempts + 1));
        Assert.That(handler.Contents.All(content => content.Disposed), Is.True);
        foreach (var request in handler.UploadRequests)
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await request.Content.ReadAsStringAsync());
            Assert.Throws<ObjectDisposedException>(() => request.Method = HttpMethod.Post);
        }
        using var ping = await client.GetAsync("https://reports.example/bdtest/ping");
        Assert.That(ping.IsSuccessStatusCode, Is.True);
    }

    private sealed class TrackingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<TrackingContent> Contents { get; } = new();
        public List<HttpRequestMessage> UploadRequests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Earlier responses, including the warm-up, must be released before another request.
            Assert.That(Contents.All(content => content.Disposed), Is.True);
            if (request.Method == HttpMethod.Post) UploadRequests.Add(request);
            var content = new TrackingContent();
            Contents.Add(content);
            return Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.OK : status) { Content = content });
        }
    }

    private sealed class TrackingContent() : StringContent("https://reports.example/bdtest/report/server-id")
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class UploadHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> upload) : HttpMessageHandler
    {
        public int UploadAttempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => request.Method == HttpMethod.Get
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                : upload(request, ++UploadAttempts);
    }
}
