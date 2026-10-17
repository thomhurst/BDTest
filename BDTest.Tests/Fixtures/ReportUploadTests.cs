using System.Collections.Concurrent;
using System.Net;
using System.Text;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportUploadTests
{
    private const string Key = "test-upload-secret";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("wrong-key")]
    public async Task MissingOrIncorrectKeyIsRejectedBeforeParsing(string key)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("not JSON", key);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(server.Store.Data, Is.Empty);
        Assert.That(server.Store.Received, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task UnconfiguredKeyDisablesUploads(string configuredKey)
    {
        await using var server = await UploadServer.Start(options => options.UploadApiKey = configuredKey);
        using var response = await server.Post(Report());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var ping = await server.Client.GetAsync("bdtest/ping");
        Assert.That(ping.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [Test]
    public async Task MultipleKeyHeadersAreRejected()
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(Report()) };
        request.Headers.Add("X-BDTest-Upload-Key", new[] { Key, Key });
        using var response = await server.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("../../existing-report")]
    [TestCase("58e394437a634373ab830636f5254892")]
    public async Task UploadsCannotReplacePersistentReportsEvenWithAnEmptyCache(string suppliedId)
    {
        await using var server = await UploadServer.Start();
        var oldId = suppliedId ?? "old-report";
        var original = new BDTestOutputModel { Id = oldId, Tag = "original" };
        server.Store.Data[oldId] = original;

        using var first = await server.Post(Report(suppliedId));
        using var second = await server.Post(Report(suppliedId));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var firstUrl = new Uri(await first.Content.ReadAsStringAsync());
        var secondUrl = new Uri(await second.Content.ReadAsStringAsync());
        Assert.That(firstUrl, Is.Not.EqualTo(secondUrl));
        Assert.That(server.Store.Data[oldId], Is.SameAs(original));
        Assert.That(server.Store.Data.Count, Is.EqualTo(3));
        foreach (var report in server.Store.Received)
        {
            Assert.That(Guid.TryParseExact(report.Id, "N", out _), Is.True);
            Assert.That(report.Id, Is.Not.EqualTo(suppliedId));
            Assert.That(server.Store.Data[report.Id], Is.SameAs(report));
            Assert.That(server.Store.Summaries.ContainsKey(report.Id), Is.True);
        }
        Assert.That(server.Store.Received.Count, Is.EqualTo(2));
        Assert.That(server.Store.Received.Any(report => firstUrl.AbsolutePath.Contains(report.Id)), Is.True);
    }

    [TestCase("null")]
    [TestCase("not JSON")]
    public async Task InvalidBodiesDoNotReachStorage(string body)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OversizedBodiesAreRejectedIncludingChunkedRequests(bool chunked)
    {
        await using var server = await UploadServer.Start();
        var json = JsonConvert.SerializeObject(new { Tag = new string('x', 10 * 1024 * 1024) });
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(json) };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        using var response = await server.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(server.Store.Data, Is.Empty);
        Assert.That(server.Store.Received, Is.Empty);
    }

    [Test]
    public async Task RateLimitIsSharedAcrossConcurrentRequestsAndDoesNotAffectReads()
    {
        await using var server = await UploadServer.Start(options => options.UploadsPerMinute = 2);
        using var unauthorized = await server.Post(Report(), "wrong");
        Assert.That(unauthorized.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => server.Post(Report())));
        try
        {
            Assert.That(responses.Count(response => response.StatusCode == HttpStatusCode.OK), Is.EqualTo(2));
            var rejected = responses.Where(response => response.StatusCode == HttpStatusCode.TooManyRequests).ToArray();
            Assert.That(rejected.Length, Is.EqualTo(6));
            Assert.That(rejected.All(response => response.Headers.RetryAfter?.Delta > TimeSpan.Zero), Is.True);
            Assert.That(server.Store.Received.Count, Is.EqualTo(2));
            using var malformed = await server.Post("not JSON");
            Assert.That(malformed.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            using var ping = await server.Client.GetAsync("bdtest/ping");
            Assert.That(ping.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    private static string Report(string id = null) => JsonConvert.SerializeObject(new
    {
        id, Tag = "uploaded", TestTimer = new { TestsStartedAt = DateTime.UtcNow, TestsFinishedAt = DateTime.UtcNow }
    });

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class UploadServer(WebApplication app, HttpClient client, RecordingStore store) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public RecordingStore Store { get; } = store;

        public static async Task<UploadServer> Start(Action<BDTestReportServerOptions> configure = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
            var app = builder.Build();
            var store = new RecordingStore();
            app.UseBDTestReportServer(options =>
            {
                options.UploadApiKey = Key;
                options.DataStore = store;
                options.DataReceiver = store;
                configure?.Invoke(options);
            });
            app.MapControllers();
            await app.StartAsync();
            return new UploadServer(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }, store);
        }

        public async Task<HttpResponseMessage> Post(string body, string key = Key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(body) };
            if (key != null) request.Headers.Add("X-BDTest-Upload-Key", key);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class RecordingStore : IBDTestDataStore, IBDTestDataReceiver
    {
        public ConcurrentDictionary<string, BDTestOutputModel> Data { get; } = new();
        public ConcurrentDictionary<string, TestRunSummary> Summaries { get; } = new();
        public ConcurrentBag<BDTestOutputModel> Received { get; } = new();
        public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken) => Task.FromResult(Data.GetValueOrDefault(id));
        public Task StoreTestData(string id, BDTestOutputModel data) { Data[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Data.TryRemove(id, out _); return Task.CompletedTask; }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult<IEnumerable<TestRunSummary>>(Summaries.Values.ToArray());
        public Task StoreTestRunRecord(TestRunSummary summary) { Summaries[summary.RecordId] = summary; return Task.CompletedTask; }
        public Task DeleteTestRunRecord(string id) { Summaries.TryRemove(id, out _); return Task.CompletedTask; }
        public Task OnReceiveTestDataAsync(BDTestOutputModel model) { Received.Add(model); return Task.CompletedTask; }
    }
}
