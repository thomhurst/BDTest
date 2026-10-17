using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportUploadTests
{
    private const string Key = "test-upload-secret";

    [TestCase("bdtest/ping", HttpStatusCode.OK)]
    [TestCase("bdtest/report/missing", HttpStatusCode.Found)]
    [TestCase("bdtest/report/missing/raw-json-data", HttpStatusCode.NotFound)]
    [TestCase("bdtest/not-a-route", HttpStatusCode.NotFound)]
    public async Task ReportResponsesHaveSecurityHeaders(string path, HttpStatusCode status)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(status));
        AssertSecurityHeaders(response);
    }

    [Test]
    public async Task ReportScriptsUseTheResponseNonceAndRequestsDoNotReuseIt()
    {
        await using var server = await UploadServer.Start();
        string previousNonce = null;
        for (var i = 0; i < 2; i++)
        {
            using var response = await server.Client.GetAsync("bdtest/report/missing/summary");
            var nonce = AssertSecurityHeaders(response);
            Assert.That(nonce, Is.Not.EqualTo(previousNonce));
            previousNonce = nonce;
            var html = await response.Content.ReadAsStringAsync();
            AssertReportScripts(html, nonce);
        }
    }

    private static void AssertReportScripts(string html, string nonce)
    {
        var scripts = Regex.Matches(html, @"<script\b[^>]*>");
        Assert.That(scripts, Is.Not.Empty);
        foreach (Match script in scripts)
        {
            var attribute = Regex.Match(script.Value, @"nonce=""([^""]+)""");
            Assert.That(HttpUtility.HtmlDecode(attribute.Groups[1].Value), Is.EqualTo(nonce), script.Value);
        }
        Assert.That(Regex.IsMatch(html, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase), Is.False);
    }

    private static string AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.That(response.Headers.GetValues("X-Content-Type-Options"), Is.EqualTo(new[] { "nosniff" }));
        Assert.That(response.Headers.GetValues("Referrer-Policy"), Is.EqualTo(new[] { "no-referrer" }));
        Assert.That(response.Headers.GetValues("X-Frame-Options"), Is.EqualTo(new[] { "DENY" }));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.That(policy, Does.Contain("frame-ancestors 'none'"));
        Assert.That(policy, Does.Contain("base-uri 'none'"));
        Assert.That(policy, Does.Contain("object-src 'none'"));
        Assert.That(policy, Does.Contain("form-action 'self'"));
        Assert.That(policy, Does.Contain("script-src-attr 'none'"));
        var scripts = Regex.Match(policy, @"(?:^|; )script-src ([^;]+)").Groups[1].Value;
        Assert.That(scripts, Does.Not.Contain("'unsafe-inline'").And.Not.Contain("'unsafe-eval'"));
        var nonce = Regex.Match(scripts, "'nonce-([^']+)'").Groups[1].Value;
        Assert.That(Convert.FromBase64String(nonce), Has.Length.GreaterThanOrEqualTo(16));
        return nonce;
    }

    [Test]
    public async Task SecurityHeadersRespectPathBaseAndLeaveHostRoutesAlone()
    {
        await using var server = await UploadServer.Start(configureApp: app =>
        {
            app.UsePathBase("/reports");
            app.MapGet("/host", () => "Host content");
        });
        using var report = await server.Client.GetAsync("reports/bdtest/ping");
        AssertSecurityHeaders(report);
        using var host = await server.Client.GetAsync("reports/host");
        Assert.That(host.Headers.Contains("Content-Security-Policy"), Is.False);
        using var similarPrefix = await server.Client.GetAsync("bdtest-other");
        Assert.That(similarPrefix.Headers.Contains("Content-Security-Policy"), Is.False);
    }

    [Test]
    public async Task HostsCanDisableBuiltInHeaders()
    {
        await using var server = await UploadServer.Start(options => options.EnableSecurityHeaders = false);
        using var response = await server.Client.GetAsync("bdtest/ping");
        Assert.That(response.Headers.Contains("Content-Security-Policy"), Is.False);
        Assert.That(response.Headers.Contains("X-Content-Type-Options"), Is.False);
    }

    [Test]
    public async Task ExistingHostPolicyRemainsEnforced()
    {
        await using var server = await UploadServer.Start(configureApp: app => app.Use(async (context, next) =>
        {
            context.Response.Headers["Content-Security-Policy"] = "img-src 'none'";
            await next();
        }));
        using var response = await server.Client.GetAsync("bdtest/ping");
        var policies = response.Headers.GetValues("Content-Security-Policy").ToArray();
        Assert.That(policies, Has.Some.EqualTo("img-src 'none'"));
        Assert.That(policies, Has.Some.Contains("script-src 'nonce-"));
    }

    [Test]
    public async Task RejectedUploadsAndHandledErrorsHaveSecurityHeaders()
    {
        await using var server = await UploadServer.Start(configureApp: app =>
        {
            app.UseExceptionHandler(error => error.Run(context =>
            {
                context.Response.StatusCode = 500;
                return context.Response.WriteAsync("Error");
            }));
            app.MapGet("/bdtest/test-error", (HttpContext _) => Task.FromException(new InvalidOperationException("Test error")));
        });
        using var rejected = await server.Post("not json", key: null);
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        AssertSecurityHeaders(rejected);
        using var error = await server.Client.GetAsync("bdtest/test-error");
        Assert.That(error.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        AssertSecurityHeaders(error);
    }

    [TestCase("application/json")]
    [TestCase("text/html")]
    public async Task MissingRawJsonReportReturns404WithoutHtml(string accept)
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Get, "bdtest/report/missing/raw-json-data");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("text/html"));
    }

    [Test]
    public async Task ExistingRawJsonReportReturnsReportAsJson()
    {
        await using var server = await UploadServer.Start();
        using var upload = await server.Post(Report());
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        using var response = await server.Client.GetAsync($"bdtest/report/{report.Id}/raw-json-data");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        var data = JsonConvert.DeserializeObject<BDTestOutputModel>(await response.Content.ReadAsStringAsync());
        Assert.That(data.Id, Is.EqualTo(report.Id));
        Assert.That(data.Tag, Is.EqualTo("uploaded"));
    }

    [Test]
    public async Task MissingReportSummaryStillRendersHtml()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Client.GetAsync("bdtest/report/missing/summary");

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("doesn't seem to exist!"));
    }

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
        // Let Kestrel reject a known oversized Content-Length before the client writes
        // the body into a closed connection. Chunked requests still stream the body.
        request.Headers.ExpectContinue = true;
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

    [TestCase("{\"TestTimer\":{}}")]
    [TestCase("{}")]
    [TestCase("{\"Scenarios\":null}")]
    [TestCase("{\"NotRun\":null}")]
    [TestCase("{\"TestTimer\":null}")]
    [TestCase("{\"Scenarios\":null,\"NotRun\":null,\"TestTimer\":null}")]
    public async Task IncompleteUploadsAreStoredAndRenderedAsEmptyReports(string json)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        Assert.That(report.Scenarios, Is.Empty);
        Assert.That(report.NotRun, Is.Empty);
        Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(default(DateTime)));
        Assert.That(report.TestTimer.TestsFinishedAt, Is.EqualTo(default(DateTime)));
        var summary = server.Store.Summaries[report.Id];
        Assert.That(summary.Counts.Total, Is.Zero);
        Assert.That(summary.Counts.NotRun, Is.Zero);
        Assert.That(summary.Status, Is.EqualTo(Status.Inconclusive));
        Assert.That(summary.StartedAtDateTime, Is.EqualTo(default(DateTime)));
        Assert.That(summary.FinishedAtDateTime, Is.EqualTo(default(DateTime)));
        await AssertReportPagesRender(server, report.Id);
    }

    [TestCase("TestsStartedAt")]
    [TestCase("TestsFinishedAt")]
    public async Task PartialTimersPreserveSuppliedTimestampAndDefaultMissingTimestamp(string member)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(
            "{\"TestTimer\":{\"" + member + "\":\"2026-01-01T12:00:00Z\"}}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[report.Id];
        var supplied = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expectedStart = member == "TestsStartedAt" ? supplied : default;
        var expectedFinish = member == "TestsFinishedAt" ? supplied : default;
        Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(expectedStart));
        Assert.That(report.TestTimer.TestsFinishedAt, Is.EqualTo(expectedFinish));
        Assert.That(summary.StartedAtDateTime, Is.EqualTo(expectedStart));
        Assert.That(summary.FinishedAtDateTime, Is.EqualTo(expectedFinish));
        await AssertReportPagesRender(server, report.Id);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IncompleteStoredReportsAreNormalizedBeforeRendering(bool bulkRead)
    {
        await using var server = await UploadServer.Start();
        // Simulate a legacy report loaded from persistent storage without an upload.
        server.Store.Data["legacy"] = new BDTestOutputModel { Id = "legacy", Scenarios = null, NotRun = null };
        if (bulkRead)
        {
            using var response = await server.Client.GetAsync("bdtest/report/test-run-times?reportIds=legacy");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        }
        else
        {
            await AssertReportPagesRender(server, "legacy");
        }
    }

    [Test]
    public async Task PopulatedUploadsKeepTheirCountsStatusAndTimestamps()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("""
            {"Scenarios":[{"Status":"Passed","Steps":[]},{"Status":"Failed","Steps":[]}],
             "NotRun":[{}],"TestTimer":{"TestsStartedAt":"2026-01-01T12:00:00Z","TestsFinishedAt":"2026-01-01T12:01:00Z"}}
            """);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[report.Id];
        Assert.That(summary.Counts.Total, Is.EqualTo(2));
        Assert.That(summary.Counts.Passed, Is.EqualTo(1));
        Assert.That(summary.Counts.Failed, Is.EqualTo(1));
        Assert.That(summary.Counts.NotRun, Is.EqualTo(1));
        Assert.That(summary.Status, Is.EqualTo(Status.Failed));
        Assert.That(summary.StartedAtDateTime, Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.That(summary.FinishedAtDateTime, Is.EqualTo(new DateTime(2026, 1, 1, 12, 1, 0, DateTimeKind.Utc)));
        Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(summary.StartedAtDateTime));
        Assert.That(report.TestTimer.TestsFinishedAt, Is.EqualTo(summary.FinishedAtDateTime));
        Assert.That(report.Scenarios, Has.Count.EqualTo(2));
        Assert.That(report.NotRun, Has.Count.EqualTo(1));
    }

    [TestCase("{}", false, false)]
    [TestCase("{\"Scenarios\":null,\"NotRun\":null}", false, false)]
    [TestCase("{\"Scenarios\":null,\"NotRun\":[{}]}", false, true)]
    [TestCase("{\"Scenarios\":[{\"Status\":\"Failed\",\"Steps\":[]}],\"NotRun\":null}", true, false)]
    public void MetricsTreatNullCollectionsAsEmpty(string json, bool failed, bool warnings)
    {
        var metrics = new OutputMetrics(JsonConvert.DeserializeObject<BDTestOutputModel>(json));
        Assert.That(metrics.AnyFailed, Is.EqualTo(failed));
        Assert.That(metrics.AllPassed, Is.EqualTo(!failed));
        Assert.That(metrics.AnyWarnings, Is.EqualTo(warnings));
        Assert.That(metrics.AnyPassed, Is.False);
        Assert.That(metrics.AnyExceptions, Is.False);
        Assert.That(metrics.AnySkipped, Is.False);
        Assert.That(metrics.AnyInconclusive, Is.False);
        Assert.That(metrics.AnyNotImplemented, Is.False);
    }

    private static async Task AssertReportPagesRender(UploadServer server, string id)
    {
        foreach (var page in new[] { "summary", "stories", "all-scenarios", "warnings", "timings", "top-defects" })
        {
            using var response = await server.Client.GetAsync($"bdtest/report/{id}/{page}");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), page);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"), page);
            var nonce = AssertSecurityHeaders(response);
            var html = await response.Content.ReadAsStringAsync();
            AssertReportScripts(html, nonce);
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

        public static async Task<UploadServer> Start(Action<BDTestReportServerOptions> configure = null, Action<WebApplication> configureApp = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
            var app = builder.Build();
            var store = new RecordingStore();
            configureApp?.Invoke(app);
            app.UseBDTestReportServer(options =>
            {
                options.UploadApiKey = Key;
                options.DataStore = store;
                options.DataReceiver = store;
                configure?.Invoke(options);
            });
            app.MapControllers();
            await app.StartAsync();
            return new UploadServer(app, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) }, store);
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
