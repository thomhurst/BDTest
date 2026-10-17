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
        var requests = new List<string>();
        using var handler = new UploadHandler(async (request, attempt) =>
        {
            requests.Add(await request.Content.ReadAsStringAsync());
            Assert.That(request.Headers.GetValues("X-BDTest-Upload-Key"), Is.EqualTo(new[] { "secret" }));
            if (attempt == 1)
            {
                retryAt = useDate
                    ? DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2)
                    : DateTimeOffset.UtcNow.AddSeconds(1);
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = useDate
                    ? new RetryConditionHeaderValue(retryAt)
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return response;
            }

            Assert.That(DateTimeOffset.UtcNow, Is.GreaterThanOrEqualTo(retryAt));
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
        using var handler = new UploadHandler((_, attempt) =>
        {
            if (attempt > 1) return Task.FromResult(Success());
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (header != null) response.Headers.TryAddWithoutValidation("Retry-After", header);
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client);

        Assert.That(handler.UploadAttempts, Is.EqualTo(2));
    }

    [Test]
    public void PersistentRateLimitStopsAfterThreeAttemptsWithoutWaitingAfterTheLast()
    {
        using var handler = new UploadHandler((_, attempt) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(attempt == 3 ? TimeSpan.FromHours(1) : TimeSpan.Zero);
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(handler.UploadAttempts, Is.EqualTo(3));
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("https://reports.example/bdtest/report/server-id")
    };

    private sealed class UploadHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> upload) : HttpMessageHandler
    {
        public int UploadAttempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => request.Method == HttpMethod.Get
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                : upload(request, ++UploadAttempts);
    }
}
