using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Storage;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Assertions.Enums;

namespace BDTest.ReportGenerator.Tests;

public class AzureAttachmentReportTests
{
    private const string UploadKey = "azure-attachment-test-key";

    [Test]
    public async Task Uploaded_Azure_attachments_render_under_their_owner_and_download_after_restart()
    {
        using var storage = new BlobTransport();
        using var storageClient = new HttpClient(storage);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(storageClient) };
        options.Retry.MaxRetries = 0;
        var store = new AzureStorageDataStore(
            new BlobContainerClient(new Uri("https://storage.test/test-runs-summaries"), options),
            new BlobContainerClient(new Uri("https://storage.test/test-data"), options));
        var imageId = Guid.NewGuid().ToString("N");
        var logId = Guid.NewGuid().ToString("N");
        var missingId = Guid.NewGuid().ToString("N");
        byte[] image = [137, 80, 78, 71, 13, 10, 26, 10, 0, 255];
        byte[] log = Encoding.UTF8.GetBytes("captured scenario log\n");
        var json = ReportJson.SerializeObject(new
        {
            Id = "client-supplied-id",
            Scenarios = new[]
            {
                new
                {
                    Guid = "azure-scenario", ScenarioText = "Azure attachments", Status = "Passed",
                    Attachments = new[]
                    {
                        new { Id = logId, Name = "scenario.log", MimeType = "text/plain", Length = log.Length, AttemptNumber = 1 },
                        new { Id = missingId, Name = "missing.txt", MimeType = "text/plain", Length = 5, AttemptNumber = 1 }
                    },
                    Steps = new[]
                    {
                        new
                        {
                            StepText = "When a screenshot is captured", Status = "Passed",
                            Attachments = new[]
                            {
                                new { Id = imageId, Name = "screenshot.png", MimeType = "image/png", Length = image.Length, AttemptNumber = 1 }
                            }
                        }
                    }
                }
            }
        });

        string reportId;
        await using (var uploader = await StartServer(store))
        {
            using var client = uploader.GetTestClient();
            client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", UploadKey);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var receipt = await client.PostAsync("/bdtest/data", content);
            await Assert.That(receipt.StatusCode).IsEqualTo(HttpStatusCode.OK);
            reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
            var token = receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single();
            await Assert.That(reportId).IsNotEqualTo("client-supplied-id");
            foreach (var (id, bytes) in new[] { (imageId, image), (logId, log) })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/bdtest/data/{reportId}/attachments/{id}")
                {
                    Content = new ByteArrayContent(bytes)
                };
                request.Headers.Add("X-BDTest-Attachment-Token", token);
                using var uploaded = await client.SendAsync(request);
                await Assert.That(uploaded.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
                await Assert.That(storage.Blobs[$"/test-data/{reportId}/attachments/{id}"])
                    .IsEquivalentTo(bytes, CollectionOrdering.Matching);
            }
        }

        // A new repository/cache must load the report manifest back through Azure's SDK.
        await using var reader = await StartServer(store);
        using var browser = reader.GetTestClient();
        var html = await browser.GetStringAsync($"/bdtest/report/{reportId}/scenario/azure-scenario");
        await Assert.That(storage.Downloads).Contains($"/test-data/{reportId}");
        var step = Regex.Matches(html, "<details\\b[^>]*class=\"[^\"]*step-item[^\"]*\"[^>]*>.*?</details>", RegexOptions.Singleline)
            .Single(match => match.Value.Contains("a screenshot is captured", StringComparison.Ordinal)).Value;
        await Assert.That(step).Contains($"data-attachment-id=\"{imageId}\"");
        await Assert.That(step).Contains("screenshot.png");
        await Assert.That(step.Contains($"data-attachment-id=\"{logId}\"", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html).Contains($"data-attachment-id=\"{logId}\"");
        await Assert.That(html).Contains("scenario.log");
        await Assert.That(html).Contains($"data-attachment-id=\"{missingId}\"");
        await Assert.That(html.Contains("client-supplied-id/attachments", StringComparison.Ordinal)).IsFalse();

        foreach (var (id, bytes, mediaType, name) in new[]
                 { (imageId, image, "image/png", "screenshot.png"), (logId, log, "text/plain", "scenario.log") })
        {
            var url = $"/bdtest/report/{reportId}/attachments/{id}";
            await Assert.That(html).Contains($"href=\"{url}\"");
            if (id == imageId) await Assert.That(step).Contains($"src=\"{url}\"");
            using var download = await browser.GetAsync(url);
            await Assert.That(download.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await download.Content.ReadAsByteArrayAsync()).IsEquivalentTo(bytes, CollectionOrdering.Matching);
            await Assert.That(download.Content.Headers.ContentType?.MediaType).IsEqualTo(mediaType);
            await Assert.That(download.Content.Headers.ContentDisposition?.DispositionType).IsEqualTo("attachment");
            await Assert.That(download.Content.Headers.ContentDisposition?.FileNameStar).IsEqualTo(name);
            await Assert.That(download.Headers.GetValues("X-Content-Type-Options").Single()).IsEqualTo("nosniff");
        }

        using var missing = await browser.GetAsync($"/bdtest/report/{reportId}/attachments/{missingId}");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(storage.Downloads).Contains($"/test-data/{reportId}/attachments/{missingId}");
        // Missing content must not prevent the scenario page and its remaining files rendering.
        await Assert.That(await browser.GetStringAsync($"/bdtest/report/{reportId}/scenario/azure-scenario"))
            .Contains($"href=\"/bdtest/report/{reportId}/attachments/{imageId}\"");
    }

    private static async Task<WebApplication> StartServer(AzureStorageDataStore store)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IReportStore>(store);
        builder.Services.AddSingleton<IRunIndexStore>(store);
        builder.Services.AddSingleton<IBDTestAttachmentStore>(store);
        builder.Services.AddBDTestReports(options =>
        {
            options.AllowAnonymousReports = true;
            options.UploadApiKey = UploadKey;
        });
        var app = builder.Build();
        app.UseBDTestReportServer();
        app.MapBDTestReports();
        await app.StartAsync();
        return app;
    }

    /// <summary>In-memory Azure HTTP transport; production Blob clients still serialize every operation.</summary>
    private sealed class BlobTransport : HttpMessageHandler
    {
        public ConcurrentDictionary<string, byte[]> Blobs { get; } = new();
        public ConcurrentBag<string> Downloads { get; } = new();
        private readonly ConcurrentDictionary<string, byte[]> _blocks = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Expected an Azure request URI.");
            var path = uri.AbsolutePath;
            if (request.Method == HttpMethod.Put)
            {
                var content = await (request.Content ?? throw new InvalidOperationException("Expected blob content."))
                    .ReadAsByteArrayAsync(cancellationToken);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                query.TryGetValue("comp", out var operation);
                if (operation == "block")
                    _blocks[path + ":" + query["blockid"]] = content;
                else if (operation == "blocklist")
                {
                    var blockIds = XDocument.Parse(Encoding.UTF8.GetString(content)).Root!.Elements().Select(element => element.Value);
                    Blobs[path] = blockIds.SelectMany(id => _blocks[path + ":" + id]).ToArray();
                }
                else Blobs[path] = content;
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Headers.ETag = new EntityTagHeaderValue("\"test-etag\"");
                response.Headers.Add("x-ms-request-server-encrypted", "true");
                return response;
            }
            if (request.Method != HttpMethod.Get) throw new InvalidOperationException($"Unexpected Azure method {request.Method}.");
            Downloads.Add(path);
            if (!Blobs.TryGetValue(path, out var bytes))
            {
                var missing = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("<Error><Code>BlobNotFound</Code><Message>Missing test blob</Message></Error>")
                };
                missing.Headers.Add("x-ms-error-code", "BlobNotFound");
                return missing;
            }
            var download = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            download.Headers.ETag = new EntityTagHeaderValue("\"test-etag\"");
            download.Headers.Add("x-ms-blob-type", "BlockBlob");
            download.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            return download;
        }
    }
}
