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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public partial class ReportUploadTests
{
    private const string Key = "test-upload-secret";

    [Test]
    [Arguments("text/plain")]
    [Arguments("application/x-www-form-urlencoded")]
    [Arguments("multipart/form-data")]
    public async Task Browser_simple_content_types_cannot_upload_reports_even_with_a_valid_API_key(string mediaType)
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = mediaType == "multipart/form-data"
                ? new MultipartFormDataContent { { new StringContent("{\"Tag\":\"cross-site\"}"), "report" } }
                : new StringContent("{\"Tag\":\"cross-site\"}", Encoding.UTF8, mediaType)
        };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.Add("Origin", "https://untrusted.example");
        using var response = await server.Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnsupportedMediaType);
        await Assert.That(server.Store.Data).IsEmpty();
        await Assert.That(server.Store.Summaries).IsEmpty();
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task UploadLimitsMustBePositive(long value)
    {
        await using var server = await UploadServer.Start();
        await Assert.That(server.Options.MaxCompressedUploadBytes).IsEqualTo(10 * 1024 * 1024);
        await Assert.That(server.Options.MaxUploadBytes).IsEqualTo(10 * 1024 * 1024);
        var services = new ServiceCollection();
        services.AddBDTestReports(options =>
        {
            options.MaxCompressedUploadBytes = value;
            options.MaxUploadBytes = value;
        });
        using var provider = services.BuildServiceProvider();
        await Assert.That(() =>
            _ = provider.GetRequiredService<IOptions<BDTestReportOptions>>().Value).ThrowsExactly<OptionsValidationException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That(Required(request.Content).Headers.ContentLength ?? throw new AssertionException("Expected content length")).IsGreaterThan(server.Options.MaxCompressedUploadBytes);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsNotEmpty();
        await Assert.That(server.Store.Data).IsEmpty();
        await Assert.That(server.Store.Summaries).IsEmpty();
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ExpandedLimitRejectsJsonBeforeStorage(bool chunked, bool utf16)
    {
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = 10_000;
            options.MaxUploadBytes = 256;
        });
        using var request = GzipRequest(ReportJson.SerializeObject(new { Tag = new string('x', 16_384) }), chunked, utf16);
        await Assert.That(Required(request.Content).Headers.ContentLength ?? throw new AssertionException("Expected content length")).IsLessThan(server.Options.MaxCompressedUploadBytes);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("MaxUploadBytes");
        await Assert.That(server.Store.Data).IsEmpty();
        await Assert.That(server.Store.Summaries).IsEmpty();
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    [MatrixDataSource]
    public async Task Utf16UploadsHonorTranscodedUtf8Limit(
        [Matrix(false, true)] bool gzip,
        [Matrix(false, true)] bool chunked,
        [Matrix(0, 1)] int bytesOverLimit)
    {
        var tag = new string('漢', 128);
        // Keep literal CJK characters: escaping them would make UTF-16 larger than UTF-8.
        var json = "{\"Tag\":\"" + tag + "\"}";
        var limit = Encoding.UTF8.GetByteCount(json) - bytesOverLimit;
        await Assert.That(Encoding.Unicode.GetByteCount(json)).IsLessThan(limit);
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
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(server.Store.Received.Single().Tag).IsEqualTo(tag);
            await Assert.That(server.Store.Data).Count().IsEqualTo(1);
            await Assert.That(server.Store.Summaries).Count().IsEqualTo(1);
        }
        else
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
            await Assert.That(await response.Content.ReadAsStringAsync()).Contains("MaxUploadBytes");
            await Assert.That(server.Store.Data).IsEmpty();
            await Assert.That(server.Store.Summaries).IsEmpty();
            await Assert.That(server.Store.Received).IsEmpty();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    public async Task CompressedWireLimitIncludesTrailingBytesInChunkedRequests()
    {
        await using var server = await UploadServer.Start(options => options.MaxCompressedUploadBytes = 1024);
        using var request = GzipRequest("{}", chunked: true);
        var gzipBytes = await Required(request.Content).ReadAsByteArrayAsync();
        Required(request.Content).Dispose();
        var padded = new byte[16_384];
        gzipBytes.CopyTo(padded, 0);
        request.Content = new ByteArrayContent(padded);
        Required(request.Content).Headers.ContentType = new("application/json");
        Required(request.Content).Headers.ContentEncoding.Add("gzip");
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(server.Store.Received.Single().Tag).IsEqualTo(tag);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ExactUploadLimitsAreInclusive(bool gzip, bool chunked)
    {
        const string json = "{\"Tag\":\"café 漢字\"}";
        using var request = gzip ? GzipRequest(json, chunked) : new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(json) };
        if (!gzip) request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        await using var server = await UploadServer.Start(options =>
        {
            options.MaxCompressedUploadBytes = Required(request.Content).Headers.ContentLength ?? throw new AssertionException("Expected upload content length.");
            options.MaxUploadBytes = Encoding.UTF8.GetByteCount(json);
        });
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(server.Store.Received.Single().Tag).IsEqualTo("café 漢字");
    }

    [Test]
    [Arguments("br")]
    [Arguments("identity")]
    [Arguments("gzip, br")]
    [Arguments("gzip, gzip")]
    public async Task UnsupportedContentEncodingsReturn415(string encoding)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest("{}");
        Required(request.Content).Headers.ContentEncoding.Clear();
        Required(request.Content).Headers.TryAddWithoutValidation("Content-Encoding", encoding);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnsupportedMediaType);
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    [Arguments("not gzip", false)]
    [Arguments("not JSON", true)]
    public async Task MalformedGzipAndJsonReturn400(string body, bool compress)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest(body);
        if (!compress)
        {
            Required(request.Content).Dispose();
            request.Content = Json(body);
            Required(request.Content).Headers.ContentEncoding.Add("gzip");
        }
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(compress ? "Invalid report JSON" : "Invalid gzip");
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("wrong-key")]
    public async Task GzipAuthenticationRunsBeforeLimitsAndDecompression(string? key)
    {
        await using var server = await UploadServer.Start(options => options.MaxCompressedUploadBytes = 1);
        using var request = GzipRequest("{}");
        request.Headers.Remove("X-BDTest-Upload-Key");
        if (key != null) request.Headers.Add("X-BDTest-Upload-Key", key);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    public async Task GzipRateLimitRunsBeforeDecompression()
    {
        await using var server = await UploadServer.Start(options => options.UploadsPerMinute = 1);
        using var first = await server.Post("{}");
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var request = GzipRequest("not JSON");
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(response.Headers.RetryAfter?.Delta ?? throw new AssertionException("Expected retry delay")).IsGreaterThan(TimeSpan.Zero);
        await Assert.That(server.Store.Received.Count).IsEqualTo(1);
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
        Required(request.Content).Headers.ContentEncoding.Add("gzip");
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("gzip:untouched");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task GzipUploadsPreserveJsonAndUtf16Support(bool chunked, bool utf16)
    {
        await using var server = await UploadServer.Start();
        using var request = GzipRequest("{\"Tag\":\"café 漢字\"}", chunked, utf16);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(server.Store.Received.Single().Tag).IsEqualTo("café 漢字");
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
        Required(request.Content).Headers.ContentType = new("application/json") { CharSet = utf16 ? "utf-16" : "utf-8" };
        Required(request.Content).Headers.ContentEncoding.Add("gzip");
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.TransferEncodingChunked = chunked;
        request.Headers.ExpectContinue = true;
        return request;
    }

    [Test]
    public async Task Host_accepts_versioned_production_uploads_and_renders_them()
    {
        using var run = BDTestRun.Start();
        await using var server = await UploadServer.Start();
        server.Client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", Key);
        await new VersionedFixture(run).When(() => { }).Then(() => { }).BDTestAsync();
        var uri = await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress!, null, server.Client, run);
        var report = server.Store.Received.Single();
        await Assert.That(report.SchemaVersion).IsEqualTo(1);
        await Assert.That(report.Scenarios).Count().IsEqualTo(1);
        await Assert.That(uri.AbsolutePath).Contains(report.Id);
        await AssertReportPagesRender(server, report.Id);
    }

    [Test]
    [Arguments("0")]
    [Arguments("2")]
    [Arguments("1.5")]
    [Arguments("null")]
    [Arguments("\"1\"")]
    public async Task Unsupported_schema_uploads_are_rejected_before_storage(string schema)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("{\"schemaVersion\":" + schema
            + ",\"id\":\"untrusted\",\"producerVersion\":\"future-producer\"}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(server.Store.Received).IsEmpty();
        await Assert.That(server.Store.Data).IsEmpty();
    }

    private sealed class VersionedFixture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [NotInParallel]
    public async Task LegacyPayloadRoundTripsThroughServer(bool injectedClient)
    {
        using var run = BDTestRun.Start();
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
        Required(scenario.Steps).Add(scenario.Steps[0]);
        run.Store.ScenariosByInternalId.TryAdd(Required(scenario.Guid), scenario);
        var descriptor = new BDTestRunDescriptor { Tag = "round-trip", BranchName = "test-branch", Environment = "test-env" };
        try
        {
            var helperJson = BDTestJsonHelper.GetTestJsonData(descriptor, run);

            await Assert.That(helperJson).DoesNotContain("\"$id\"").And.DoesNotContain("\"$ref\"");
            var helperReport = BDTestJsonHelper.DeserializeReport(helperJson);
            var helperScenario = Required(helperReport.Scenarios).Single(item => item.Guid == scenario.Guid);
            await Assert.That(Required(helperScenario.Steps)[1]).IsNotSameReferenceAs(helperScenario.Steps[0]);

            var uri = injectedClient
                ? await BDTest.Tests.Helpers.LegacyHostUpload.SendAsync(Required(server.Client.BaseAddress), descriptor, server.Client, run)
                : await SendLegacyWithOwnedClient();

            async Task<Uri> SendLegacyWithOwnedClient()
            {
                using var client = new HttpClient();
                return await BDTest.Tests.Helpers.LegacyHostUpload.SendAsync(Required(server.Client.BaseAddress), descriptor, client, run);
            }
            var received = server.Store.Received.Single();
            var receivedScenario = Required(received.Scenarios).Single(item => item.Guid == scenario.Guid);
            await Assert.That(Required(receivedScenario.Steps)[1].StepText).IsEqualTo(receivedScenario.Steps[0].StepText);
            await Assert.That(receivedScenario.Steps[0].StepText).IsEqualTo("A shared step");
            await Assert.That(received.Tag).IsEqualTo(helperReport.Tag);
            await Assert.That(received.BranchName).IsEqualTo(helperReport.BranchName);
            await Assert.That(received.Environment).IsEqualTo(helperReport.Environment);
            await Assert.That(uri.AbsolutePath).Contains(Required(received.Id));
            if (!injectedClient)
            {
                // Disposing the internally owned client also closes its pooled transport connection.
                await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            run.Store.ScenariosByInternalId.TryRemove(scenario.Guid, out _);
        }
    }

    [Test]
    public async Task Cache_only_oversized_upload_is_rejected_before_receiver_notification()
    {
        await using var server = await UploadServer.Start(options => options.ReportCacheSizeLimitBytes = 1,
            configureServices: services => services.RemoveAll<IBDTestDataStore>());

        using var response = await server.Post("{}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(server.Store.Received).IsEmpty();
        await Assert.That(server.Store.Summaries).IsEmpty();
    }

    [Test]
    public async Task InternallyOwnedClientClosesConnectionAfterFailedUpload()
    {
        using var run = BDTestRun.Start();
        var connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await UploadServer.Start(connectionClosed: () => connectionClosed.TrySetResult());

        // The convenience overload has no upload key, so the server rejects its request.
        await Assert.That(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(run, Required(server.Client.BaseAddress), new BDTestRunDescriptor())).ThrowsExactly<HttpRequestException>();

        await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    [Arguments("bdtest/ping", HttpStatusCode.OK)]
    [Arguments("bdtest/report/missing", HttpStatusCode.Found)]
    [Arguments("bdtest/report/missing/raw-json-data", HttpStatusCode.NotFound)]
    [Arguments("bdtest/not-a-route", HttpStatusCode.NotFound)]
    public async Task ReportResponsesHaveSecurityHeaders(string path, HttpStatusCode status)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Client.GetAsync(path);
        await Assert.That(response.StatusCode).IsEqualTo(status);
        await AssertSecurityHeaders(response);
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
        await Assert.That(images).IsEqualTo("'self' data: https: http://images.example:8080 https://pictures.example http://[::1]:8081 http://xn--bcher-kva.example");
        await Assert.That(policy).Contains("connect-src 'self';");
        await Assert.That(policy).Contains("script-src 'nonce-");
        await Assert.That(policy).DoesNotContain("script-src 'unsafe-inline'");
        await Assert.That(server.Options.AdditionalImageSources[0]).IsEqualTo("http://images.example:8080");
    }

    [Test]
    [Arguments("bdtest/ping", false)]
    [Arguments("bdtest/assets/css/bdtest-report.css", true)]
    public async Task ConfiguredImagePolicyPreservesResponseCaching(string path, bool isAsset)
    {
        await using var server = await UploadServer.Start(options =>
            options.AdditionalImageSources = new[] { "http://images.example" });
        using var response = await server.Client.GetAsync(path);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.GetValues("Content-Security-Policy").Single()).Contains("img-src 'self' data: https: http://images.example;");
        var cache = Required(response.Headers.CacheControl);
        await Assert.That(cache).IsNotNull();
        await Assert.That(cache.NoStore).IsEqualTo(!isAsset);
        if (isAsset)
        {
            await Assert.That(cache.Public).IsTrue();
            await Assert.That(cache.MaxAge).IsEqualTo(TimeSpan.FromDays(1));
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("http:")]
    [Arguments("*")]
    [Arguments("http://*.example.com")]
    [Arguments("https://*.example.com")]
    [Arguments("http://*")]
    [Arguments("http://images.example:*")]
    [Arguments("'unsafe-inline'")]
    [Arguments("ftp://images.example")]
    [Arguments("http://user:password@images.example")]
    [Arguments("http://images.example/path")]
    [Arguments("http://images.example/..")]
    [Arguments("http://images.example/path/../")]
    [Arguments("http://images.example?query=value")]
    [Arguments("http://images.example#fragment")]
    [Arguments("http://images.example;script-src")]
    [Arguments("http://images.example; script-src 'unsafe-inline'")]
    [Arguments("http://images.example https://other.example")]
    [Arguments("http://images.example\r\nX-Injected: value")]
    [Arguments(" http://images.example")]
    public async Task InvalidImageOriginsAreRejectedWithoutChangingThePolicy(string? source)
    {
        await using var server = await UploadServer.Start();
        await Assert.That(() => server.Options.AdditionalImageSources = new[] { "http://valid.example", source! }).ThrowsExactly<ArgumentException>();
        await Assert.That(server.Options.AdditionalImageSources).IsEmpty();
        using var response = await server.Client.GetAsync("bdtest/ping");
        await AssertSecurityHeaders(response);
    }

    [Test]
    public async Task NullImageSourcesAreRejected()
    {
        await using var server = await UploadServer.Start();
        await Assert.That(() => server.Options.AdditionalImageSources = null!).ThrowsExactly<ArgumentNullException>();
        await Assert.That(server.Options.AdditionalImageSources).IsEmpty();
    }

    [Test]
    public async Task ReportScriptsUseTheResponseNonceAndRequestsDoNotReuseIt()
    {
        await using var server = await UploadServer.Start();
        string? previousNonce = null;
        for (var i = 0; i < 2; i++)
        {
            using var response = await server.Client.GetAsync("bdtest/report/missing/summary");
            var nonce = await AssertSecurityHeaders(response);
            await Assert.That(nonce).IsNotEqualTo(previousNonce);
            previousNonce = nonce;
            var html = await response.Content.ReadAsStringAsync();
            await AssertReportScripts(html, nonce);
        }
    }

    private static async Task AssertReportScripts(string html, string nonce)
    {
        var scripts = Regex.Matches(html, @"<script\b[^>]*>");
        await Assert.That(scripts).IsNotEmpty();
        foreach (Match script in scripts)
        {
            var attribute = Regex.Match(script.Value, @"nonce=""([^""]+)""");
            await Assert.That(HttpUtility.HtmlDecode(attribute.Groups[1].Value)).IsEqualTo(nonce).Because(script.Value);
        }
        await Assert.That(Regex.IsMatch(html, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase)).IsFalse();
    }

    private static async Task<string> AssertSecurityHeaders(HttpResponseMessage response)
    {
        await Assert.That(response.Headers.GetValues("X-Content-Type-Options")).IsEquivalentTo(new[] { "nosniff" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(response.Headers.GetValues("Referrer-Policy")).IsEquivalentTo(new[] { "no-referrer" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(response.Headers.GetValues("X-Frame-Options")).IsEquivalentTo(new[] { "DENY" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(response.Headers.CacheControl?.NoStore).IsTrue();
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        await Assert.That(policy).Contains("frame-ancestors 'none'");
        await Assert.That(policy).Contains("base-uri 'none'");
        await Assert.That(policy).Contains("object-src 'none'");
        await Assert.That(policy).Contains("form-action 'self'");
        await Assert.That(policy).Contains("script-src-attr 'none'");
        var images = Regex.Match(policy, @"(?:^|; )img-src ([^;]+)").Groups[1].Value;
        await Assert.That(images).IsEqualTo("'self' data: https:");
        var scripts = Regex.Match(policy, @"(?:^|; )script-src ([^;]+)").Groups[1].Value;

        await Assert.That(scripts).DoesNotContain("'unsafe-inline'").And.DoesNotContain("'unsafe-eval'");
        var nonce = Regex.Match(scripts, "'nonce-([^']+)'").Groups[1].Value;

        await Assert.That((Convert.FromBase64String(nonce)).Length).IsGreaterThanOrEqualTo(16);
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
        await AssertSecurityHeaders(report);
        using var host = await server.Client.GetAsync("reports/host");
        await Assert.That(host.Headers.Contains("Content-Security-Policy")).IsFalse();
        using var similarPrefix = await server.Client.GetAsync("bdtest-other");
        await Assert.That(similarPrefix.Headers.Contains("Content-Security-Policy")).IsFalse();
    }

    [Test]
    public async Task HostsCanDisableBuiltInHeaders()
    {
        await using var server = await UploadServer.Start(options => options.EnableSecurityHeaders = false);
        using var response = await server.Client.GetAsync("bdtest/ping");
        await Assert.That(response.Headers.Contains("Content-Security-Policy")).IsFalse();
        await Assert.That(response.Headers.Contains("X-Content-Type-Options")).IsFalse();
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

        await Assert.That((policies).Any(item => Equals(item, "img-src 'none'"))).IsTrue();

        await Assert.That((policies).Any(item => item.Contains("script-src 'nonce-"))).IsTrue();
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
        await Assert.That(rejected.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await AssertSecurityHeaders(rejected);
        using var error = await server.Client.GetAsync("bdtest/test-error");
        await Assert.That(error.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await AssertSecurityHeaders(error);
    }

    [Test]
    [Arguments("application/json")]
    [Arguments("text/html")]
    public async Task MissingRawJsonReportReturns404WithoutHtml(string accept)
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Get, "bdtest/report/missing/raw-json-data");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await server.Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEmpty();
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsNotEqualTo("text/html");
    }

    [Test]
    public async Task ExistingRawJsonReportReturnsReportAsJson()
    {
        await using var server = await UploadServer.Start();
        using var upload = await server.Post(Report());
        await Assert.That(upload.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        using var response = await server.Client.GetAsync($"bdtest/report/{report.Id}/raw-json-data");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        var data = ReportJson.DeserializeObject<BDTest.Contracts.BDTestReport>(await response.Content.ReadAsStringAsync());
        await Assert.That(data.Id).IsEqualTo(report.Id);
        await Assert.That(data.Tag).IsEqualTo("uploaded");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HistoricalReportRendersWithoutModernOptionalFields(bool references)
    {
        await using var server = await UploadServer.Start();
        var json = JObject.Parse(HistoricalReportFixture.ReadJson());
        if (references)
        {
            var steps = (JArray)Required(json.SelectToken("Scenarios[0].Steps"));
            steps.Add(new JObject { ["$ref"] = Required(steps[0]).Value<string>("$id") });
        }
        using var upload = await server.Post(json.ToString());
        await Assert.That(upload.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        await Assert.That(Required(server.Store.Summaries[Required(report.Id)].Counts).Total).IsEqualTo(13);

        await AssertReportPagesRender(server, report.Id);
        // Render every historical scenario, including failures and unexecuted steps with null output.
        foreach (var scenario in Required(report.Scenarios))
        {
            using var response = await server.Client.GetAsync($"bdtest/report/{report.Id}/scenario/{scenario.Guid}");
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(scenario.ScenarioText);
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
            // Compare visible text: step keywords are styled separately from the rest of the step.
            var markup = await response.Content.ReadAsStringAsync();
            await Assert.That(markup).DoesNotContain("Retry history");
            var html = HttpUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(markup, "<[^>]+>", string.Empty));
            await Assert.That(html).Contains(scenario.ScenarioText);
            foreach (var step in Required(scenario.Steps))
            {
                await Assert.That(html).Contains(Required(step.StepText));
                if (step.Exception != null)
                    await Assert.That(html).Contains(Required(Required(step.Exception).TypeName));
            }
            if (!string.IsNullOrEmpty(scenario.TestStartupInformation))
                await Assert.That(html).Contains(Required(scenario.TestStartupInformation).TrimEnd());
            if (!string.IsNullOrEmpty(scenario.TearDownOutput))
                await Assert.That(html).Contains(Required(scenario.TearDownOutput).TrimEnd());
            await Assert.That(html).DoesNotContain("Retry history");
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
        await Assert.That(upload.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        await Assert.That(report.BranchName).IsEqualTo("branch");
        await Assert.That(Required(report.Scenarios).Single().StoryText).IsEqualTo("story");
        using var raw = await server.Client.GetAsync($"bdtest/report/{report.Id}/raw-json-data");
        await Assert.That(raw.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var downloaded = Required(BDTest.Razor.Reports.Serialization.HostedReportJson.Deserialize(await raw.Content.ReadAsStringAsync()));
        await Assert.That(downloaded.BranchName).IsEqualTo("branch");
        await Assert.That(Required(downloaded.Scenarios).Single().FrameworkTestId).IsEqualTo("test");
    }

    [Test]
    public async Task MissingReportSummaryStillRendersHtml()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Client.GetAsync("bdtest/report/missing/summary");

        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("This run is no longer stored");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("wrong-key")]
    public async Task MissingOrIncorrectKeyIsRejectedBeforeParsing(string? key)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("not JSON", key);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(server.Store.Data).IsEmpty();
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UnconfiguredKeyDisablesUploads(string? configuredKey)
    {
        await using var server = await UploadServer.Start(options => options.UploadApiKey = configuredKey);
        using var response = await server.Post(Report());
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        using var ping = await server.Client.GetAsync("bdtest/ping");
        await Assert.That(ping.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    public async Task MultipleKeyHeadersAreRejected()
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data") { Content = Json(Report()) };
        request.Headers.Add("X-BDTest-Upload-Key", new[] { Key, Key });
        using var response = await server.Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("../../existing-report")]
    [Arguments("58e394437a634373ab830636f5254892")]
    public async Task UploadsCannotReplacePersistentReportsEvenWithAnEmptyCache(string? suppliedId)
    {
        await using var server = await UploadServer.Start();
        var oldId = suppliedId ?? "old-report";
        var original = new BDTest.Contracts.BDTestReport(1, oldId, "tests", tag: "original");
        server.Store.Data[oldId] = original;

        using var first = await server.Post(Report(suppliedId));
        using var second = await server.Post(Report(suppliedId));
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var firstUrl = new Uri(await first.Content.ReadAsStringAsync());
        var secondUrl = new Uri(await second.Content.ReadAsStringAsync());
        await Assert.That(firstUrl).IsNotEqualTo(secondUrl);
        await Assert.That(server.Store.Data[oldId]).IsSameReferenceAs(original);
        await Assert.That(server.Store.Data.Count).IsEqualTo(3);
        foreach (var report in server.Store.Received)
        {
            await Assert.That(Guid.TryParseExact(report.Id, "N", out _)).IsTrue();
            await Assert.That(report.Id).IsNotEqualTo(suppliedId);
            await Assert.That(BDTest.Helpers.BDTestJsonHelper.Serialize(Required(server.Store.Data[Required(report.Id)]))).IsEqualTo(BDTest.Helpers.BDTestJsonHelper.Serialize(report));
            await Assert.That(server.Store.Summaries.ContainsKey(report.Id)).IsTrue();
        }
        await Assert.That(server.Store.Received.Count).IsEqualTo(2);
        await Assert.That(server.Store.Received.Any(report => firstUrl.AbsolutePath.Contains(Required(report.Id)))).IsTrue();
    }

    [Test]
    [Arguments("null")]
    [Arguments("not JSON")]
    [Arguments("{\"Scenarios\":[{\"StoryText\":123}]}")]
    [Arguments("{\"Scenarios\":[{\"ScenarioText\":[]}]}")]
    [Arguments("{\"Scenarios\":[{\"CustomTestInformation\":[true]}]}")]
    public async Task InvalidBodiesDoNotReachStorage(string body)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(server.Store.Data).IsEmpty();
    }

    [Test]
    [Arguments("application/json", false, HttpStatusCode.OK)]
    [Arguments("text/json", false, HttpStatusCode.OK)]
    [Arguments("application/vnd.bdtest+json", false, HttpStatusCode.OK)]
    [Arguments("application/json", true, HttpStatusCode.OK)]
    [Arguments("text/plain", false, HttpStatusCode.UnsupportedMediaType)]
    [Arguments("text/example+json", false, HttpStatusCode.UnsupportedMediaType)]
    public async Task Upload_reader_preserves_MVC_JSON_media_types_and_encodings(string mediaType, bool utf16, HttpStatusCode expected)
    {
        await using var server = await UploadServer.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = new StringContent("{\"Tag\":\"café 漢字\"}", utf16 ? Encoding.Unicode : Encoding.UTF8, mediaType)
        };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        using var response = await server.Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(expected);
        if (expected == HttpStatusCode.OK)
        {
            await Assert.That(server.Store.Received.Single().Tag).IsEqualTo("café 漢字");
        }
        else
        {
            await Assert.That(server.Store.Received).IsEmpty();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.RequestEntityTooLarge);
        await Assert.That(server.Store.Data).IsEmpty();
        await Assert.That(server.Store.Received).IsEmpty();
    }

    [Test]
    public async Task RateLimitIsSharedAcrossConcurrentRequestsAndDoesNotAffectReads()
    {
        await using var server = await UploadServer.Start(options => options.UploadsPerMinute = 2);
        using var unauthorized = await server.Post(Report(), "wrong");
        await Assert.That(unauthorized.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => server.Post(Report())));
        try
        {
            await Assert.That(responses.Count(response => response.StatusCode == HttpStatusCode.OK)).IsEqualTo(2);
            var rejected = responses.Where(response => response.StatusCode == HttpStatusCode.TooManyRequests).ToArray();
            await Assert.That(rejected.Length).IsEqualTo(6);
            await Assert.That(rejected.All(response => response.Headers.RetryAfter?.Delta > TimeSpan.Zero)).IsTrue();
            await Assert.That(server.Store.Received.Count).IsEqualTo(2);
            using var malformed = await server.Post("not JSON");
            await Assert.That(malformed.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
            using var ping = await server.Client.GetAsync("bdtest/ping");
            await Assert.That(ping.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Test]
    [Arguments("{\"TestTimer\":{}}")]
    [Arguments("{}")]
    [Arguments("{\"Scenarios\":null}")]
    [Arguments("{\"NotRun\":null}")]
    [Arguments("{\"CustomProperties\":null}")]
    [Arguments("{\"TestTimer\":null}")]
    [Arguments("{\"Scenarios\":null,\"NotRun\":null,\"TestTimer\":null}")]
    public async Task IncompleteUploadsAreStoredAndRenderedAsEmptyReports(string json)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(json);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        await Assert.That(report.Scenarios).IsEmpty();
        await Assert.That(report.NotRun).IsEmpty();
        await Assert.That(report.TestTimer?.TestsStartedAt ?? default).IsEqualTo(default(DateTimeOffset));
        await Assert.That(report.TestTimer?.TestsFinishedAt ?? default).IsEqualTo(default(DateTimeOffset));
        var summary = server.Store.Summaries[Required(report.Id)];
        await Assert.That(Required(summary.Counts).Total).IsZero();
        await Assert.That(Required(summary.Counts).NotRun).IsZero();
        await Assert.That(summary.Status).IsEqualTo(Status.NotRun);
        await Assert.That(summary.StartedAtDateTime).IsEqualTo(default(DateTime));
        await Assert.That(summary.FinishedAtDateTime).IsEqualTo(default(DateTime));
        await AssertReportPagesRender(server, report.Id);
    }

    [Test]
    public async Task Uploads_normalize_legacy_null_collections_to_immutable_empty_collections()
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post("""
            {"Scenarios":[{"Steps":null,"Examples":null,"ExampleValues":null,
              "Attachments":null,"Attempts":null}]}
            """);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var scenario = Required(server.Store.Received.Single().Scenarios).Single();
        await Assert.That(scenario.Steps).IsEmpty();
        await Assert.That(scenario.Examples).IsEmpty();
        await Assert.That(scenario.ExampleValues).IsEmpty();
        await Assert.That(scenario.Attachments).IsEmpty();
        await Assert.That(scenario.Attempts).IsEmpty();
    }

    [Test]
    [Arguments("TestsStartedAt")]
    [Arguments("TestsFinishedAt")]
    public async Task PartialTimersPreserveSuppliedTimestampAndDefaultMissingTimestamp(string member)
    {
        await using var server = await UploadServer.Start();
        using var response = await server.Post(
            "{\"TestTimer\":{\"" + member + "\":\"2026-01-01T12:00:00Z\"}}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[Required(report.Id)];
        var supplied = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expectedStart = member == "TestsStartedAt" ? supplied : default;
        var expectedFinish = member == "TestsFinishedAt" ? supplied : default;
        await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(expectedStart);
        await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsEqualTo(expectedFinish);
        await Assert.That(summary.StartedAtDateTime).IsEqualTo(expectedStart);
        await Assert.That(summary.FinishedAtDateTime).IsEqualTo(expectedFinish);
        await AssertReportPagesRender(server, report.Id);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IncompleteStoredReportsAreNormalizedBeforeRendering(bool bulkRead)
    {
        await using var server = await UploadServer.Start();
        // Simulate a legacy report loaded from persistent storage without an upload.
        server.Store.Data["legacy"] = new BDTest.Contracts.BDTestReport(1, "legacy", "tests");
        if (bulkRead)
        {
            using var response = await server.Client.GetAsync("bdtest/report/test-run-times?reportIds=legacy");
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[Required(report.Id)];
        await Assert.That(Required(summary.Counts).Total).IsEqualTo(2);
        await Assert.That(Required(summary.Counts).Passed).IsEqualTo(1);
        await Assert.That(Required(summary.Counts).Failed).IsEqualTo(1);
        await Assert.That(Required(summary.Counts).NotRun).IsEqualTo(1);
        await Assert.That(summary.Status).IsEqualTo(Status.Failed);
        await Assert.That(summary.StartedAtDateTime).IsEqualTo(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        await Assert.That(summary.FinishedAtDateTime).IsEqualTo(new DateTime(2026, 1, 1, 12, 1, 0, DateTimeKind.Utc));
        await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(summary.StartedAtDateTime);
        await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsEqualTo(summary.FinishedAtDateTime);
        await Assert.That(report.Scenarios).Count().IsEqualTo(2);
        await Assert.That(report.NotRun).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("{}", false, false)]
    [Arguments("{\"Scenarios\":null,\"NotRun\":null}", false, false)]
    [Arguments("{\"Scenarios\":null,\"NotRun\":[{}]}", false, true)]
    [Arguments("{\"Scenarios\":[{\"Status\":\"Failed\",\"Steps\":[]}],\"NotRun\":null}", true, false)]
    public async Task MetricsTreatNullCollectionsAsEmpty(string json, bool failed, bool warnings)
    {
        var metrics = new OutputMetrics((ReportJson.DeserializeObject<BDTestOutputModel>(json)).ToReport());
        await Assert.That(metrics.AnyFailed).IsEqualTo(failed);
        await Assert.That(metrics.AllPassed).IsEqualTo(!failed);
        await Assert.That(metrics.AnyWarnings).IsEqualTo(warnings);
        await Assert.That(metrics.AnyPassed).IsFalse();
        await Assert.That(metrics.AnyExceptions).IsFalse();
        await Assert.That(metrics.AnySkipped).IsFalse();
        await Assert.That(metrics.AnyInconclusive).IsFalse();
        await Assert.That(metrics.AnyNotImplemented).IsFalse();
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var report = server.Store.Received.Single();
        var summary = server.Store.Summaries[Required(report.Id)];
        await Assert.That(Required(summary.Counts).NotRun).IsEqualTo(2);
        await Assert.That(Required(summary.Counts).Pending).IsEqualTo(1);
        await Assert.That(Required(summary.Counts).Inconclusive).IsEqualTo(1);
        await Assert.That(summary.Status).IsEqualTo(Status.Inconclusive);
        var metrics = new OutputMetrics(report);
        await Assert.That(metrics.AnyNotRun).IsTrue();
        await Assert.That(metrics.AnyPending).IsTrue();
        await Assert.That(metrics.AllPassed).IsFalse();
        await AssertReportPagesRender(server, report.Id);
        foreach (var status in new[] { Status.NotRun, Status.Pending })
        {
            var html = await server.Client.GetStringAsync($"bdtest/report/{report.Id}/all-scenarios?filterByStatus={status}");
            await Assert.That(html).Contains(status == Status.NotRun ? "Not run" : "Pending");
            await Assert.That(html).Contains($"scenario-id=\"{(status == Status.NotRun ? "not-run" : "running")}\"");
            await Assert.That(html).DoesNotContain("scenario-id=\"inconclusive\"");
        }
    }

    private static async Task AssertReportPagesRender(UploadServer server, string id)
    {
        foreach (var page in new[] { "summary", "stories", "all-scenarios", "warnings", "timings", "top-defects" })
        {
            using var response = await server.Client.GetAsync($"bdtest/report/{id}/{page}");
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(page);
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html").Because(page);
            var nonce = await AssertSecurityHeaders(response);
            var html = await response.Content.ReadAsStringAsync();
            await AssertReportScripts(html, nonce);
        }
    }

    private static string Report(string? id = null) => ReportJson.SerializeObject(new
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

        public static async Task<UploadServer> Start(Action<BDTestReportOptions>? configure = null, Action<WebApplication>? configureApp = null,
            Action? connectionClosed = null, Action<System.Text.Json.JsonSerializerOptions>? configureJson = null,
            Action<IServiceCollection>? configureServices = null)
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

        public async Task<HttpResponseMessage> Post(string body, string? key = Key)
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
        public ConcurrentDictionary<string, BDTest.Contracts.BDTestReport?> Data { get; } = new();
        public ConcurrentDictionary<string, TestRunSummary> Summaries { get; } = new();
        public ConcurrentBag<BDTest.Contracts.BDTestReport> Received { get; } = new();
        public Task<BDTest.Contracts.BDTestReport?> GetTestData(string id, CancellationToken cancellationToken) => Task.FromResult(Data.GetValueOrDefault(id));
        public Task StoreTestData(string id, BDTest.Contracts.BDTestReport? data) { Data[id] = data; return Task.CompletedTask; }
        public Task DeleteTestData(string id) { Data.TryRemove(id, out _); return Task.CompletedTask; }
        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() => Task.FromResult<IEnumerable<TestRunSummary>>(Summaries.Values.ToArray());
        public Task StoreTestRunRecord(TestRunSummary summary) { Summaries[Required(summary.RecordId)] = summary; return Task.CompletedTask; }
        public Task DeleteTestRunRecord(string id) { Summaries.TryRemove(id, out _); return Task.CompletedTask; }
        public Task OnReceiveTestDataAsync(BDTest.Contracts.BDTestReport model) { Received.Add(model); return Task.CompletedTask; }
    }
}
