using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using BDTest.TestUtilities;
using BDTest.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BDTest.Tests.Fixtures;

public partial class ReportUploadTests
{
    private const string Key = "test-upload-secret";

    [TestCase(0)]
    [TestCase(-1)]
    public async Task UploadLimitsMustBePositive(long value)
    {
        await using var server = await UploadServer.Start();
        Assert.That(server.Options.MaxCompressedUploadBytes, Is.EqualTo(10 * 1024 * 1024));
        Assert.That(server.Options.MaxUploadBytes, Is.EqualTo(10 * 1024 * 1024));
        var services = new ServiceCollection();
        services.AddBDTestReports(options =>
        {
            options.MaxCompressedUploadBytes = value;
            options.MaxUploadBytes = value;
        });
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<BDTestReportOptions>>().Value);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CompressedWireLimitRejectsKnownAndChunkedBodies(bool chunked)
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 1024;
            options.MaxUploadBytes = 100_000;
        });
        var random = new byte[16_384];
        new Random(882).NextBytes(random);
        using var request = GzipRequest(ReportJson.SerializeObject(new { Tag = Convert.ToBase64String(random) }), chunked);
        Assert.That(request.Content.Headers.ContentLength, Is.GreaterThan(server.Options.MaxCompressedUploadBytes));
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Not.Empty);
        Assert.That(server.Store.Data, Is.Empty);
        Assert.That(server.Store.Summaries, Is.Empty);
        Assert.That(server.Store.Received, Is.Empty);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ExpandedLimitRejectsJsonBeforeStorage(bool chunked, bool utf16)
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 10_000;
            options.MaxUploadBytes = 256;
        });
        using var request = GzipRequest(ReportJson.SerializeObject(new { Tag = new string('x', 16_384) }), chunked, utf16);
        Assert.That(request.Content.Headers.ContentLength, Is.LessThan(server.Options.MaxCompressedUploadBytes));
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("MaxUploadBytes"));
        Assert.That(server.Store.Data, Is.Empty);
        Assert.That(server.Store.Summaries, Is.Empty);
        Assert.That(server.Store.Received, Is.Empty);
    }

    [Test]
    public async Task Utf16UploadsHonorTranscodedUtf8Limit(
        [Values(false, true)] bool gzip,
        [Values(false, true)] bool chunked,
        [Values(0, 1)] int bytesOverLimit)
    {
        var tag = new string('漢', 128);
        // Keep literal CJK characters: escaping them would make UTF-16 larger than UTF-8.
        var json = "{\"Tag\":\"" + tag + "\"}";
        var limit = Encoding.UTF8.GetByteCount(json) - bytesOverLimit;
        Assert.That(Encoding.Unicode.GetByteCount(json), Is.LessThan(limit));
        await using var server = await UploadServer.Start(options => options.MaxUploadBytes = limit);
        using var request = gzip ? GzipRequest(json, chunked, utf16: true) : new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = new StringContent(json, Encoding.Unicode, "application/json")
        };
        if (!gzip) request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        using var response = await server.Client.SendAsync(request);

        if (bytesOverLimit == 0)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(server.Store.Received.Single().Tag, Is.EqualTo(tag));
            Assert.That(server.Store.Data, Has.Count.EqualTo(1));
            Assert.That(server.Store.Summaries, Has.Count.EqualTo(1));
        }
        else
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("MaxUploadBytes"));
            Assert.That(server.Store.Data, Is.Empty);
            Assert.That(server.Store.Summaries, Is.Empty);
            Assert.That(server.Store.Received, Is.Empty);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PlainJsonAlsoHonorsExpandedLimit(bool chunked)
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 10_000;
            options.MaxUploadBytes = 128;
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = Json(ReportJson.SerializeObject(new { Tag = new string('x', 256) }))
        };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(server.Store.Received, Is.Empty);
    }

    [Test]
    public async Task CompressedWireLimitIncludesTrailingBytesInChunkedRequests()
    {
        await using var server = await UploadServer.Start(options => options.MaxCompressedUploadBytes = 1024);
        using var request = GzipRequest("{}", chunked: true);
        var gzipBytes = await request.Content.ReadAsByteArrayAsync();
        request.Content.Dispose();
        var padded = new byte[16_384];
        gzipBytes.CopyTo(padded, 0);
        request.Content = new ByteArrayContent(padded);
        request.Content.Headers.ContentType = new("application/json");
        request.Content.Headers.ContentEncoding.Add("gzip");
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(server.Store.Received, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HostsCanRaiseUploadLimitsAboveThePreviousFixedLimit(bool gzip)
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 12 * 1024 * 1024;
            options.MaxUploadBytes = 12 * 1024 * 1024;
        });
        var tag = new string('x', 11 * 1024 * 1024);
        var json = ReportJson.SerializeObject(new { Tag = tag });
        using var request = gzip ? GzipRequest(json) : new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(json) };
        if (!gzip) request.Headers.Add("X-BDTest-Upload-Key", Key);
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(server.Store.Received.Single().Tag, Is.EqualTo(tag));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ExactUploadLimitsAreInclusive(bool gzip, bool chunked)
    {
        const string json = "{\"Tag\":\"café 漢字\"}";
        using var request = gzip ? GzipRequest(json, chunked) : new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(json) };
        if (!gzip) request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = request.Content.Headers.ContentLength.Value;
            options.MaxUploadBytes = Encoding.UTF8.GetByteCount(json);
        });
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(server.Store.Received.Single().Tag, Is.EqualTo("café 漢字"));
    }

    [TestCase("br")]
    [TestCase("identity")]
    [TestCase("gzip, br")]
    [TestCase("gzip, gzip")]
    public async Task UnsupportedContentEncodingsReturn415(string encoding)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest("{}");
        request.Content.Headers.ContentEncoding.Clear();
        request.Content.Headers.TryAddWithoutValidation("Content-Encoding", encoding);
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [TestCase("not gzip", false)]
    [TestCase("not JSON", true)]
    public async Task MalformedGzipAndJsonReturn400(string body, bool compress)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest(body);
        if (!compress)
        {
            request.Content.Dispose();
            request.Content = Json(body);
            request.Content.Headers.ContentEncoding.Add("gzip");
        }
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(compress ? "Invalid report JSON" : "Invalid gzip"));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("wrong-key")]
    public async Task GzipAuthenticationRunsBeforeLimitsAndDecompression(string key)
    {
        await using var server = await UploadServer.Start(options => options.MaxCompressedUploadBytes = 1);
        using var request = GzipRequest("{}");
        request.Headers.Remove("X-BDTest-Upload-Key");
        if (key != null) request.Headers.Add("X-BDTest-Upload-Key", key);
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [Test]
    public async Task GzipRateLimitRunsBeforeDecompression()
    {
        await using var server = await UploadServer.Start(options => options.UploadsPerMinute = 1);
        using var first = await server.Post("{}");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var request = GzipRequest("not JSON");
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(response.Headers.RetryAfter?.Delta, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(server.Store.Received.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadLimitsAndDecompressionLeaveHostEndpointsAlone()
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 1;
            options.MaxUploadBytes = 1;
        }, configureApp: app => app.MapPost("/host", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            await context.Response.WriteAsync(context.Request.Headers.ContentEncoding + ":" + await reader.ReadToEndAsync());
        }));
        using var request = new HttpRequestMessage(HttpMethod.Post, "host") { Content = Json("untouched") };
        request.Content.Headers.ContentEncoding.Add("gzip");
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("gzip:untouched"));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task GzipUploadsPreserveJsonAndUtf16Support(bool chunked, bool utf16)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest("{\"Tag\":\"café 漢字\"}", chunked, utf16);
        using var response = await server.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(server.Store.Received.Single().Tag, Is.EqualTo("café 漢字"));
    }

    private static HttpRequestMessage GzipRequest(string json, bool chunked = false, bool utf16 = false)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write((utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(json));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = new ByteArrayContent(compressed.ToArray())
        };
        request.Content.Headers.ContentType = new("application/json") { CharSet = utf16 ? "utf-16" : "utf-8" };
        request.Content.Headers.ContentEncoding.Add("gzip");
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        request.Headers.ExpectContinue = true;
        return request;
    }

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public async Task ReportClientRoundTripsPlainJsonThroughServer(bool injectedClient)
    {
        var connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Supply authentication in this test host so both client overloads use the real upload endpoint.
        await using var server = await UploadServer.Start(configureApp: app => app.Use((context, next) =>
        {
            context.Request.Headers["X-BDTest-Upload-Key"] = Key;
            return next(context);
        }), connectionClosed: () => connectionClosed.TrySetResult());
        var scenario = ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"upload-round-trip","Status":"Passed","Steps":[{"StepText":"A shared step"}]}
            """);
        scenario.Steps.Add(scenario.Steps[0]);
        TestHolder.ScenariosByInternalId.TryAdd(scenario.Guid, scenario);
        var descriptor = new BDTestRunDescriptor { Tag = "round-trip", BranchName = "test-branch", Environment = "test-env" };
        try
        {
            var helperJson = BDTestJsonHelper.GetTestJsonData(descriptor);
            Assert.That(helperJson, Does.Not.Contain("\"$id\"").And.Not.Contain("\"$ref\""));
            var helperReport = ReportJson.DeserializeObject<BDTestOutputModel>(helperJson);
            var helperScenario = helperReport.Scenarios.Single(item => item.Guid == scenario.Guid);
            Assert.That(helperScenario.Steps[1], Is.Not.SameAs(helperScenario.Steps[0]));

            var uri = injectedClient
                ? await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress, descriptor, server.Client)
                : await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress, descriptor);

            var received = server.Store.Received.Single();
            var receivedScenario = received.Scenarios.Single(item => item.Guid == scenario.Guid);
            Assert.That(receivedScenario.Steps[1].StepText, Is.EqualTo(receivedScenario.Steps[0].StepText));
            Assert.That(receivedScenario.Steps[0].StepText, Is.EqualTo("A shared step"));
            Assert.That(received.Tag, Is.EqualTo(helperReport.Tag));
            Assert.That(received.BranchName, Is.EqualTo(helperReport.BranchName));
            Assert.That(received.Environment, Is.EqualTo(helperReport.Environment));
            Assert.That(uri.AbsolutePath, Does.Contain(received.Id));
            if (!injectedClient)
            {
                // Disposing the internally owned client also closes its pooled transport connection.
                await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            TestHolder.ScenariosByInternalId.TryRemove(scenario.Guid, out _);
        }
    }

    [Test]
    public async Task Cache_only_oversized_upload_is_rejected_before_receiver_notification()
    {
        await using var server = await UploadServer.Start(options => options.ReportCacheSizeLimitBytes = 1,
            configureServices: services => services.RemoveAll<IBDTestDataStore>());

        using var response = await server.Post("{}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(server.Store.Received, Is.Empty);
        Assert.That(server.Store.Summaries, Is.Empty);
    }

    [Test]
    public async Task InternallyOwnedClientClosesConnectionAfterFailedUpload()
    {
        var connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await UploadServer.Start(connectionClosed: () => connectionClosed.TrySetResult());

        // The convenience overload has no upload key, so the server rejects its request.
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress, new BDTestRunDescriptor()));

        await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

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
    public async Task HostsCanExtendImageSourcesWithoutChangingOtherDirectives()
    {
        var sources = new[]
        {
            "http://images.example:8080/", "https://pictures.example", "http://[::1]:8081", "http://b\u00fccher.example"
        };
        await using var server = await UploadServer.Start(options => options.AdditionalImageSources = sources);
        sources[0] = "http:; script-src 'unsafe-inline'";
        using var response = await server.Client.GetAsync("bdtest/ping");
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        var images = Regex.Match(policy, @"(?:^|; )img-src ([^;]+)").Groups[1].Value;
        Assert.That(images, Is.EqualTo("'self' data: https: http://images.example:8080 https://pictures.example http://[::1]:8081 http://xn--bcher-kva.example"));
        Assert.That(policy, Does.Contain("connect-src 'self';"));
        Assert.That(policy, Does.Contain("script-src 'nonce-"));
        Assert.That(policy, Does.Not.Contain("script-src 'unsafe-inline'"));
        Assert.That(server.Options.AdditionalImageSources[0], Is.EqualTo("http://images.example:8080"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("http:")]
    [TestCase("*")]
    [TestCase("http://*.example.com")]
    [TestCase("https://*.example.com")]
    [TestCase("http://*")]
    [TestCase("http://images.example:*")]
    [TestCase("'unsafe-inline'")]
    [TestCase("ftp://images.example")]
    [TestCase("http://user:password@images.example")]
    [TestCase("http://images.example/path")]
    [TestCase("http://images.example/..")]
    [TestCase("http://images.example/path/../")]
    [TestCase("http://images.example?query=value")]
    [TestCase("http://images.example#fragment")]
    [TestCase("http://images.example;script-src")]
    [TestCase("http://images.example; script-src 'unsafe-inline'")]
    [TestCase("http://images.example https://other.example")]
    [TestCase("http://images.example\r\nX-Injected: value")]
    [TestCase(" http://images.example")]
    public async Task InvalidImageOriginsAreRejectedWithoutChangingThePolicy(string source)
    {
        await using var server = await UploadServer.Start();
        Assert.Throws<ArgumentException>(() => server.Options.AdditionalImageSources = new[] { "http://valid.example", source });
        Assert.That(server.Options.AdditionalImageSources, Is.Empty);
        using var response = await server.Client.GetAsync("bdtest/ping");
        AssertSecurityHeaders(response);
    }

    [Test]
    public async Task NullImageSourcesAreRejected()
    {
        await using var server = await UploadServer.Start();
        Assert.Throws<ArgumentNullException>(() => server.Options.AdditionalImageSources = null);
        Assert.That(server.Options.AdditionalImageSources, Is.Empty);
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
        var images = Regex.Match(policy, @"(?:^|; )img-src ([^;]+)").Groups[1].Value;
        Assert.That(images, Is.EqualTo("'self' data: https:"));
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
        var data = ReportJson.DeserializeObject<BDTestOutputModel>(await response.Content.ReadAsStringAsync());
        Assert.That(data.Id, Is.EqualTo(report.Id));
        Assert.That(data.Tag, Is.EqualTo("uploaded"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HistoricalReportRendersWithoutModernOptionalFields(bool references)
    {
        await using var server = await UploadServer.Start();
        var json = JObject.Parse(HistoricalReportFixture.ReadJson());
        if (references)
        {
            var steps = (JArray)json["Scenarios"][0]["Steps"];
            steps.Add(new JObject { ["$ref"] = steps[0].Value<string>("$id") });
        }
        using var upload = await server.Post(json.ToString());
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        Assert.That(server.Store.Summaries[report.Id].Counts.Total, Is.EqualTo(13));

        await AssertReportPagesRender(server, report.Id);
        // Render every historical scenario, including failures and unexecuted steps with null output.
        foreach (var scenario in report.Scenarios)
        {
            using var response = await server.Client.GetAsync($"bdtest/report/{report.Id}/scenario/{scenario.Guid}");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), scenario.GetScenarioText());
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            // Compare visible text: step keywords are styled separately from the rest of the step.
            var markup = await response.Content.ReadAsStringAsync();
            Assert.That(markup, Does.Not.Contain("Retry history"));
            var html = HttpUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(markup, "<[^>]+>", string.Empty));
            Assert.That(html, Does.Contain(scenario.GetScenarioText()));
            foreach (var step in scenario.Steps)
            {
                Assert.That(html, Does.Contain(step.StepText));
                if (step.Exception != null)
                    Assert.That(html, Does.Contain(step.Exception.TypeName));
            }
            if (!string.IsNullOrEmpty(scenario.TestStartupInformation))
                Assert.That(html, Does.Contain(scenario.TestStartupInformation.TrimEnd()));
            if (!string.IsNullOrEmpty(scenario.TearDownOutput))
                Assert.That(html, Does.Contain(scenario.TearDownOutput.TrimEnd()));
            Assert.That(html, Does.Not.Contain("Retry history"));
        }
    }

    [Test]
    public async Task Upload_and_raw_download_ignore_host_JSON_options()
    {
        await using var server = await UploadServer.Start(configureJson: options =>
        {
            options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
            options.IgnoreReadOnlyProperties = true;
        });
        using var upload = await server.Post("""
            {"BranchName":"branch","Scenarios":[{"Guid":"scenario","FrameworkTestId":"test","StoryText":"story","ScenarioText":"scenario","Steps":[]}]}
            """);
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        Assert.That(report.BranchName, Is.EqualTo("branch"));
        Assert.That(report.Scenarios.Single().GetStoryText(), Is.EqualTo("story"));
        using var raw = await server.Client.GetAsync($"bdtest/report/{report.Id}/raw-json-data");
        Assert.That(raw.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var downloaded = BDTestJsonHelper.Deserialize(await raw.Content.ReadAsStringAsync());
        Assert.That(downloaded.BranchName, Is.EqualTo("branch"));
        Assert.That(downloaded.Scenarios.Single().FrameworkTestId, Is.EqualTo("test"));
    }

    [Test]
    public async Task MissingReportSummaryStillRendersHtml()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Client.GetAsync("bdtest/report/missing/summary");

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("This run is no longer stored"));
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
            Assert.That(BDTest.Helpers.BDTestJsonHelper.Serialize(server.Store.Data[report.Id]),
                Is.EqualTo(BDTest.Helpers.BDTestJsonHelper.Serialize(report)));
            Assert.That(server.Store.Summaries.ContainsKey(report.Id), Is.True);
        }
        Assert.That(server.Store.Received.Count, Is.EqualTo(2));
        Assert.That(server.Store.Received.Any(report => firstUrl.AbsolutePath.Contains(report.Id)), Is.True);
    }

    [TestCase("null")]
    [TestCase("not JSON")]
    [TestCase("{\"Scenarios\":[{\"StoryText\":123}]}")]
    [TestCase("{\"Scenarios\":[{\"ScenarioText\":[]}]}")]
    [TestCase("{\"Scenarios\":[{\"CustomTestInformation\":[true]}]}")]
    public async Task InvalidBodiesDoNotReachStorage(string body)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(server.Store.Data, Is.Empty);
    }

    [TestCase("application/json", false, HttpStatusCode.OK)]
    [TestCase("text/json", false, HttpStatusCode.OK)]
    [TestCase("application/vnd.bdtest+json", false, HttpStatusCode.OK)]
    [TestCase("application/json", true, HttpStatusCode.OK)]
    [TestCase("text/plain", false, HttpStatusCode.UnsupportedMediaType)]
    [TestCase("text/example+json", false, HttpStatusCode.UnsupportedMediaType)]
    public async Task Upload_reader_preserves_MVC_JSON_media_types_and_encodings(string mediaType, bool utf16, HttpStatusCode expected)
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = new StringContent("{\"Tag\":\"café 漢字\"}", utf16 ? Encoding.Unicode : Encoding.UTF8, mediaType)
        };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        using var response = await server.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(expected));
        if (expected == HttpStatusCode.OK)
        {
            Assert.That(server.Store.Received.Single().Tag, Is.EqualTo("café 漢字"));
        }
        else
        {
            Assert.That(server.Store.Received, Is.Empty);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OversizedBodiesAreRejectedIncludingChunkedRequests(bool chunked)
    {
        await using var server = await UploadServer.Start();
        var json = ReportJson.SerializeObject(new { Tag = new string('x', 10 * 1024 * 1024) });
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
        Assert.That(summary.Status, Is.EqualTo(Status.NotRun));
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
        var metrics = new OutputMetrics(ReportJson.DeserializeObject<BDTestOutputModel>(json));
        Assert.That(metrics.AnyFailed, Is.EqualTo(failed));
        Assert.That(metrics.AllPassed, Is.EqualTo(!failed));
        Assert.That(metrics.AnyWarnings, Is.EqualTo(warnings));
        Assert.That(metrics.AnyPassed, Is.False);
        Assert.That(metrics.AnyExceptions, Is.False);
        Assert.That(metrics.AnySkipped, Is.False);
        Assert.That(metrics.AnyInconclusive, Is.False);
        Assert.That(metrics.AnyNotImplemented, Is.False);
    }

    [Test]
    public async Task UnfinishedResultsAreCountedFilteredAndRenderedSeparatelyFromInconclusive()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("""
            {"Scenarios":[
              {"Guid":"not-run","Status":"NotRun","Steps":[{"Status":"NotRun"}]},
              {"Guid":"running","Status":"Pending","Steps":[{"Status":"Pending"}]},
              {"Guid":"inconclusive","Status":"Inconclusive","Steps":[{"Status":"Inconclusive"}]}],
             "NotRun":[{}]}
            """);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[report.Id];
        Assert.That(summary.Counts.NotRun, Is.EqualTo(2));
        Assert.That(summary.Counts.Pending, Is.EqualTo(1));
        Assert.That(summary.Counts.Inconclusive, Is.EqualTo(1));
        Assert.That(summary.Status, Is.EqualTo(Status.Inconclusive));
        var metrics = new OutputMetrics(report);
        Assert.That(metrics.AnyNotRun, Is.True);
        Assert.That(metrics.AnyPending, Is.True);
        Assert.That(metrics.AllPassed, Is.False);
        await AssertReportPagesRender(server, report.Id);
        foreach (var status in new[] { Status.NotRun, Status.Pending })
        {
            var html = await server.Client.GetStringAsync($"bdtest/report/{report.Id}/all-scenarios?filterByStatus={status}");
            Assert.That(html, Does.Contain(status == Status.NotRun ? "Not run" : "Pending"));
            Assert.That(html, Does.Contain($"scenario-id=\"{(status == Status.NotRun ? "not-run" : "running")}\""));
            Assert.That(html, Does.Not.Contain("scenario-id=\"inconclusive\""));
        }
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

    private static string Report(string id = null) => ReportJson.SerializeObject(new
    {
        id, Tag = "uploaded", TestTimer = new { TestsStartedAt = DateTime.UtcNow, TestsFinishedAt = DateTime.UtcNow }
    });

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class UploadServer(WebApplication app, HttpClient client, RecordingStore store) : IAsyncDisposable
    {
        public IDataRepository Repository => app.Services.GetRequiredService<IDataRepository>();
        public HttpClient Client { get; } = client;
        public RecordingStore Store { get; } = store;
        public BDTestReportOptions Options => app.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value;

        public static async Task<UploadServer> Start(Action<BDTestReportOptions> configure = null, Action<WebApplication> configureApp = null,
            Action connectionClosed = null, Action<System.Text.Json.JsonSerializerOptions> configureJson = null,
            Action<IServiceCollection> configureServices = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen =>
                listen.Use(next => async connection =>
                {
                    try
                    {
                        await next(connection);
                    }
                    finally
                    {
                        connectionClosed?.Invoke();
                    }
                })));
            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options => configureJson?.Invoke(options.JsonSerializerOptions));
            var store = new RecordingStore();
            builder.Services.AddSingleton<IBDTestDataStore>(store);
            builder.Services.AddSingleton<IBDTestDataReceiver>(store);
            builder.Services.AddBDTestReports(options =>
            {
                options.AllowAnonymousReports = true;
                options.UploadApiKey = Key;
                configure?.Invoke(options);
            });
            configureServices?.Invoke(builder.Services);
            var app = builder.Build();
            configureApp?.Invoke(app);
            app.UseBDTestReportServer();
            app.MapBDTestReports();
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
