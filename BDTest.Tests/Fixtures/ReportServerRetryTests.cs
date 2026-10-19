using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using BDTest.Settings;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ReportServerRetryTests : TestRunOwner
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RateLimitedUploadWaitsForRetryAfter(bool useDate)
    {
        DateTimeOffset retryAt = default;
        var elapsed = new Stopwatch();
        var requests = new List<string>();
        using var handler = new UploadHandler(async (request, attempt) =>
        {
            requests.Add(await BDTest.Tests.Helpers.CompatibilityReportUpload.ReadReportAsync(Required(request.Content)));
            await Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key")).IsEquivalentTo(new[] { "secret" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
                await Assert.That(DateTimeOffset.UtcNow).IsGreaterThanOrEqualTo(retryAt);
            }
            else
            {
                // Relative delays must use a monotonic clock; UTC can be adjusted while waiting.
                await Assert.That(elapsed.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
            }
            return Success();
        });
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", "secret");

        var result = await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);

        await Assert.That(result).IsEqualTo(new Uri("https://reports.example/bdtest/report/server-id"));
        await Assert.That(handler.UploadAttempts).IsEqualTo(2);
        await Assert.That(requests[1]).IsEqualTo(requests[0]);
    }

    [Test]
    [Arguments(null)]
    [Arguments("invalid")]
    [Arguments("0")]
    [Arguments("Wed, 21 Oct 2015 07:28:00 GMT")]
    public async Task MissingInvalidOrExpiredRetryAfterDoesNotPreventRecovery(string? header)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler(async (_, attempt) =>
        {
            if (attempt > 1)
            {
                await Assert.That(elapsed.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250));
                return Success();
            }
            elapsed.Start();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (header != null) response.Headers.TryAddWithoutValidation("Retry-After", header);
            return response;
        });
        using var client = new HttpClient(handler);

        await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);

        await Assert.That(handler.UploadAttempts).IsEqualTo(2);
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

        await Assert.That(async () =>
            await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun).WaitAsync(TimeSpan.FromSeconds(5))).ThrowsExactly<HttpRequestException>();
        await Assert.That(handler.UploadAttempts).IsEqualTo(3);
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("https://reports.example/bdtest/report/server-id")
    };

    [Test]
    [Arguments(HttpStatusCode.BadRequest)]
    [Arguments(HttpStatusCode.Unauthorized)]
    [Arguments(HttpStatusCode.Forbidden)]
    [Arguments(HttpStatusCode.NotFound)]
    [Arguments(HttpStatusCode.Conflict)]
    [Arguments(HttpStatusCode.RequestEntityTooLarge)]
    [Arguments(HttpStatusCode.NotImplemented)]
    [Arguments(HttpStatusCode.HttpVersionNotSupported)]
    public async Task PermanentFailuresAreNotRetried(HttpStatusCode status)
    {
        using var handler = new UploadHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var client = new HttpClient(handler);

        await Assert.That(async () =>
            await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun)).ThrowsExactly<HttpRequestException>();

        await Assert.That(handler.UploadAttempts).IsEqualTo(1);
    }

    [Test]
    [Arguments(HttpStatusCode.RequestTimeout)]
    [Arguments(HttpStatusCode.TooManyRequests)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.BadGateway)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(HttpStatusCode.GatewayTimeout)]
    public async Task TransientFailuresBackOffAndRecover(HttpStatusCode status)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler(async (_, attempt) =>
        {
            if (attempt > 1)
            {
                await Assert.That(elapsed.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250 * (attempt - 1)));
            }
            elapsed.Restart();
            return attempt < 3 ? new HttpResponseMessage(status) : Success();
        });
        using var client = new HttpClient(handler);

        await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);

        await Assert.That(handler.UploadAttempts).IsEqualTo(3);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TransportFailuresAndTimeoutsCanRecover(bool timeout)
    {
        var elapsed = new Stopwatch();
        using var handler = new UploadHandler(async (_, attempt) =>
        {
            if (attempt > 1)
            {
                await Assert.That(elapsed.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250));
                return Success();
            }
            elapsed.Start();
            throw timeout ? new TaskCanceledException("Timeout") : new HttpRequestException("Connection reset");
        });
        using var client = new HttpClient(handler);

        await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);

        await Assert.That(handler.UploadAttempts).IsEqualTo(2);
    }

    [Test]
    public async Task ProgrammingErrorsAreNotRetried()
    {
        using var handler = new UploadHandler((_, _) => throw new InvalidOperationException("Invalid handler configuration"));
        using var client = new HttpClient(handler);

        await Assert.That(async () =>
            await BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun)).ThrowsExactly<InvalidOperationException>();

        await Assert.That(handler.UploadAttempts).IsEqualTo(1);
    }

    [Test]
    [Arguments(HttpStatusCode.OK, 1)]
    [Arguments(HttpStatusCode.BadRequest, 1)]
    [Arguments(HttpStatusCode.ServiceUnavailable, 3)]
    public async Task ResponsesAndRequestsAreDisposedButInjectedClientRemainsUsable(HttpStatusCode status, int attempts)
    {
        using var handler = new TrackingHandler(status);
        using var client = new HttpClient(handler);
        var upload = BDTest.Tests.Helpers.CompatibilityReportUpload.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);
        if (status == HttpStatusCode.OK) await upload;
        else await Assert.That(async () => await upload).ThrowsExactly<HttpRequestException>();

        await Assert.That(handler.Contents).Count().IsEqualTo(attempts);
        await Assert.That(handler.Contents.All(content => content.Disposed)).IsTrue();
        foreach (var request in handler.UploadRequests)
        {
            await Assert.That(async () => await BDTest.Tests.Helpers.CompatibilityReportUpload.ReadReportAsync(Required(request.Content))).ThrowsExactly<ObjectDisposedException>();
            await Assert.That(() => request.Method = HttpMethod.Post).ThrowsExactly<ObjectDisposedException>();
        }
        using var ping = await client.GetAsync("https://reports.example/bdtest/ping");
        await Assert.That(ping.IsSuccessStatusCode).IsTrue();
    }

    private sealed class TrackingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<TrackingContent> Contents { get; } = new();
        public List<HttpRequestMessage> UploadRequests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Earlier responses must be released before another request.
            await Assert.That(Contents.All(content => content.Disposed)).IsTrue();
            if (request.Method == HttpMethod.Post) UploadRequests.Add(request);
            var content = new TrackingContent();
            Contents.Add(content);
            return new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.OK : status) { Content = content };
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
