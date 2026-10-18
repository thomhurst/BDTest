using System.Net;
using System.Text;
using BDTest.Helpers;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Storage;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.Tests.Fixtures;

public partial class ReportUploadTests
{
    [Test]
    public async Task Stalled_body_does_not_block_other_uploads_or_deletion_and_cannot_resurrect_files()
    {
        using var files = new AttachmentDirectory();
        using var body = new BlockingAttachmentBody();
        await using var server = await UploadServer.Start(configureApp: app => app.Use(async (context, next) =>
            {
                if (context.Request.Headers.ContainsKey("X-Test-Stall")) context.Request.Body = body;
                await next(context);
            }), configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var first = await server.Post(AttachmentReport(attachmentId));
        using var second = await server.Post(AttachmentReport(attachmentId));
        var firstId = first.Headers.GetValues("X-BDTest-Report-Id").Single();
        var secondId = second.Headers.GetValues("X-BDTest-Report-Id").Single();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"bdtest/data/{firstId}/attachments/{attachmentId}")
            { Content = new ByteArrayContent(new byte[] { 1, 2 }) };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.Add("X-BDTest-Attachment-Token", first.Headers.GetValues("X-BDTest-Attachment-Token").Single());
        request.Headers.Add("X-Test-Stall", "true");
        var stalled = server.Client.SendAsync(request);
        try
        {
            await body.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var other = await PostAttachment(server, secondId, attachmentId,
                second.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 3, 4 })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            await server.Repository.DeleteReport(firstId).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { body.Continue.TrySetResult(); }
        using var rejected = await stalled;
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await files.Store.OpenReadAsync(firstId, attachmentId), Is.Null);
    }

    [Test]
    public async Task Active_store_write_does_not_block_another_reports_upload_or_cleanup()
    {
        using var files = new AttachmentDirectory();
        var blocking = new BlockingAttachmentStore(files.Store);
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(blocking));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var first = await server.Post(AttachmentReport(attachmentId));
        using var second = await server.Post(AttachmentReport(attachmentId));
        var firstId = first.Headers.GetValues("X-BDTest-Report-Id").Single();
        var secondId = second.Headers.GetValues("X-BDTest-Report-Id").Single();
        blocking.ReportToBlock = firstId;
        var upload = PostAttachment(server, firstId, attachmentId,
            first.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 1, 2 });
        try
        {
            await blocking.Writing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var other = await PostAttachment(server, secondId, attachmentId,
                second.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 3, 4 })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            await server.Repository.DeleteReport(secondId).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(await files.Store.OpenReadAsync(secondId, attachmentId), Is.Null);
        }
        finally { blocking.Continue.TrySetResult(); }
        using var completed = await upload;
        Assert.That(completed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [TestCase("file")]
    [TestCase("report")]
    [TestCase("count")]
    public async Task Lower_upload_limits_preserve_existing_downloads(string limit)
    {
        using var files = new AttachmentDirectory();
        await using var first = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await first.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        using var upload = await PostAttachment(first, reportId, attachmentId,
            receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 1, 2 });
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        await using var second = await UploadServer.Start(options =>
        {
            if (limit == "file") options.MaximumAttachmentBytes = 0;
            if (limit == "report") options.MaximumReportAttachmentBytes = 0;
            if (limit == "count") options.MaximumAttachmentsPerReport = 0;
        }, configureServices: services =>
        {
            services.AddSingleton<IBDTestDataStore>(first.Store);
            services.AddSingleton<IBDTestAttachmentStore>(files.Store);
        });
        using var download = await second.Client.GetAsync($"bdtest/report/{reportId}/attachments/{attachmentId}");
        Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await download.Content.ReadAsByteArrayAsync(), Is.EqualTo(new byte[] { 1, 2 }));
        using var rejected = await second.Post(AttachmentReport(Guid.NewGuid().ToString("N")));
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase(null)]
    [TestCase("wrong")]
    public async Task Attachment_upload_rejects_missing_or_wrong_api_key_even_with_valid_receipt(string key)
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await server.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"bdtest/data/{reportId}/attachments/{attachmentId}")
            { Content = new ByteArrayContent(new byte[] { 1, 2 }) };
        if (key != null) request.Headers.Add("X-BDTest-Upload-Key", key);
        request.Headers.Add("X-BDTest-Attachment-Token", receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single());
        using var response = await server.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(Directory.GetFiles(files.Path, "*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task Uploader_includes_outline_retry_files_and_empty_scenario_files()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        server.Client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", Key);
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, retryLimit: 1);
        using var run = BDTestRun.Start(options);
        var fixture = new AttachmentFixture(run);
        fixture.Attachments.Attach("empty.txt", Array.Empty<byte>(), "text/plain");
        var attempts = new Dictionary<int, int>();
        await fixture.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
            fixture.When(() =>
            {
                attempts.TryGetValue(row.Value, out var attempt);
                attempts[row.Value] = ++attempt;
                fixture.Attachments.Attach($"row-{row.Value}-attempt-{attempt}.txt", new byte[] { (byte)row.Value }, "text/plain");
                if (attempt == 1) throw new InvalidOperationException("retry");
            }).Then(() => { })).BDTestAsync();
        await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress, null, server.Client, run);
        var report = server.Store.Received.Single();
        var attachments = BDTestAttachmentHelper.GetAttachments(report).ToArray();
        Assert.That(attachments, Has.Length.EqualTo(5));
        Assert.That(attachments.Count(file => file.AttemptNumber == 2), Is.EqualTo(2));
        foreach (var attachment in attachments)
        {
            using var content = await files.Store.OpenReadAsync(report.Id, attachment.Id);
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Length, Is.EqualTo(attachment.Length));
        }
    }

    [Test]
    public async Task Deletion_waits_for_active_write_and_cannot_leave_resurrected_files()
    {
        using var files = new AttachmentDirectory();
        var blocking = new BlockingAttachmentStore(files.Store);
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(blocking));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await server.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        var upload = PostAttachment(server, reportId, attachmentId,
            receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 1, 2 });
        try
        {
            await blocking.Writing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var deletion = server.Repository.DeleteReport(reportId);
            Assert.That(blocking.Deleting, Is.False);
            blocking.Continue.TrySetResult();
            using var response = await upload;
            await deletion;
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await files.Store.OpenReadAsync(reportId, attachmentId), Is.Null);
        }
        finally { blocking.Continue.TrySetResult(); }
    }

    [TestCase("no store")]
    [TestCase("duplicate")]
    [TestCase("count")]
    [TestCase("total")]
    public async Task Manifest_rejects_unconfigured_storage_duplicate_ids_and_report_limits(string reason)
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(options =>
        {
            if (reason == "count") options.MaximumAttachmentsPerReport = 0;
            if (reason == "total") options.MaximumReportAttachmentBytes = 1;
        }, configureServices: services =>
        {
            if (reason != "no store") services.AddSingleton<IBDTestAttachmentStore>(files.Store);
        });
        var id = Guid.NewGuid().ToString("N");
        var json = AttachmentReport(id);
        if (reason == "duplicate")
        {
            var model = BDTestJsonHelper.Deserialize(json);
            model.Scenarios[0].Attachments.Add(model.Scenarios[0].Attachments.Single());
            json = BDTestJsonHelper.Serialize(model);
        }
        using var response = await server.Post(json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(server.Store.Received, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Attachment_retries_reopen_streams_and_failure_preserves_capture(bool permanentFailure)
    {
        using var run = BDTestRun.Start();
        var fixture = new AttachmentFixture(run);
        var scenario = await fixture.When(() => fixture.Attachments.Attach("log.txt", new byte[] { 1, 2, 3 }, "text/plain"))
            .Then(() => { }).BDTestAsync();
        var attempts = 0;
        var reports = 0;
        var reportId = Guid.NewGuid().ToString("N");
        using var handler = new AttachmentHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Get) return new HttpResponseMessage(HttpStatusCode.OK);
            if (request.RequestUri!.AbsolutePath == "/bdtest/data")
            {
                reports++;
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"https://reports.test/bdtest/report/{reportId}") };
                response.Headers.Add("X-BDTest-Report-Id", reportId);
                response.Headers.Add("X-BDTest-Attachment-Token", new string('a', 64));
                return response;
            }
            Assert.That(reports, Is.EqualTo(1));
            Assert.That(request.Content, Is.TypeOf<StreamContent>());
            Assert.That(await request.Content.ReadAsByteArrayAsync(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            attempts++;
            return new HttpResponseMessage(permanentFailure ? HttpStatusCode.Forbidden
                : attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler);
        var upload = BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.test"), null, client, run);
        if (permanentFailure) await Assert.ThrowsAsync<HttpRequestException>(async () => await upload);
        else await upload;
        Assert.That(attempts, Is.EqualTo(permanentFailure ? 1 : 2));
        using var content = run.OpenAttachment(scenario.Steps[0].Attachments.Single().Id);
        Assert.That(content.Length, Is.EqualTo(3));
    }

    [Test]
    public async Task Foreign_metadata_cannot_make_uploader_read_another_run()
    {
        using var run = BDTestRun.Start();
        using var other = BDTestRun.Start();
        var fixture = new AttachmentFixture(run);
        var scenario = await fixture.When(() => { }).Then(() => { }).BDTestAsync();
        var foreign = new AttachmentFixture(other);
        var foreignScenario = await foreign.When(() => foreign.Attachments.Attach("other", new byte[] { 1 }, "text/plain"))
            .Then(() => { }).BDTestAsync();
        scenario.Attachments.Add(foreignScenario.Steps[0].Attachments.Single());
        using var handler = new AttachmentHttpHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.test"), null, client, run));
    }

    [Test]
    public async Task Cache_only_expiry_removes_owned_files()
    {
        using var files = new AttachmentDirectory();
        var observing = new ObservingAttachmentStore(files.Store);
        await using var server = await UploadServer.Start(options =>
        {
            options.ReportCacheRetention = TimeSpan.FromMilliseconds(500);
        }, configureServices: services =>
        {
            services.RemoveAll<IBDTestDataStore>();
            services.AddSingleton<IBDTestAttachmentStore>(observing);
        });
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await server.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        using var upload = await PostAttachment(server, reportId, attachmentId,
            receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 1, 2 });
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        await Task.Delay(700);
        Assert.That(await server.Repository.GetDataForBulkRead(reportId, default), Is.Null);
        Assert.That(await observing.Deleted.Task.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(reportId));
        Assert.That(Directory.GetFiles(files.Path, "*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task Receipt_authorizes_after_persistent_report_reload_and_hash_cannot_authorize()
    {
        using var files = new AttachmentDirectory();
        await using var firstServer = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await firstServer.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        var token = receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single();
        var restored = BDTestJsonHelper.Deserialize(BDTestJsonHelper.Serialize(firstServer.Store.Received.Single()));
        Assert.That(BDTestJsonHelper.Serialize(restored), Does.Not.Contain(token));
        await using var secondServer = await UploadServer.Start(configureServices: services =>
        {
            services.AddSingleton<IBDTestDataStore>(firstServer.Store);
            services.AddSingleton<IBDTestAttachmentStore>(files.Store);
        });
        using var rejected = await PostAttachment(secondServer, reportId, attachmentId, restored.AttachmentUploadTokenHash, new byte[] { 1, 2 });
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var accepted = await PostAttachment(secondServer, reportId, attachmentId, token, new byte[] { 1, 2 });
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Chunked_overlong_input_never_reaches_store()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var receipt = await server.Post(AttachmentReport(attachmentId));
        var reportId = receipt.Headers.GetValues("X-BDTest-Report-Id").Single();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"bdtest/data/{reportId}/attachments/{attachmentId}");
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.Add("X-BDTest-Attachment-Token", receipt.Headers.GetValues("X-BDTest-Attachment-Token").Single());
        request.Headers.TransferEncodingChunked = true;
        request.Content = new ByteArrayContent(new byte[] { 1, 2, 3 });
        using var response = await server.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge));
        Assert.That(Directory.GetFiles(files.Path, "*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task Uploader_sends_only_selected_run_files_after_report_and_keeps_capture_alive()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        server.Client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", Key);
        using var run = BDTestRun.Start();
        using var other = BDTestRun.Start();
        var fixture = new AttachmentFixture(run);
        var scenario = await fixture.When(() => fixture.Attachments.Attach("screen.svg", Encoding.UTF8.GetBytes("<svg/>"), "image/svg+xml"))
            .Then(() => { }).BDTestAsync();
        var foreign = new AttachmentFixture(other);
        await foreign.When(() => foreign.Attachments.Attach("foreign", new byte[] { 9 }, "text/plain")).Then(() => { }).BDTestAsync();

        var uri = await BDTestReportServer.SendDataAndGetReportUriAsync(server.Client.BaseAddress, null, server.Client, run);
        var report = server.Store.Received.Single();
        var attachment = report.Scenarios.Single().Steps[0].Attachments.Single();
        using var response = await server.Client.GetAsync($"bdtest/report/{report.Id}/attachments/{attachment.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("<svg/>"));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/svg+xml"));
        Assert.That(response.Content.Headers.ContentDisposition!.DispositionType, Is.EqualTo("attachment"));
        Assert.That(response.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
        Assert.That(uri.AbsolutePath, Does.Contain(report.Id));
        Assert.That(Directory.GetFiles(files.Path, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        using var captured = run.OpenAttachment(scenario.Steps[0].Attachments.Single().Id);
        Assert.That(captured.Length, Is.EqualTo(6));
    }

    [Test]
    public async Task Attachment_upload_requires_report_token_manifest_and_exact_length()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var first = await server.Post(AttachmentReport(attachmentId));
        using var second = await server.Post(AttachmentReport(attachmentId));
        var firstId = first.Headers.GetValues("X-BDTest-Report-Id").Single();
        var secondId = second.Headers.GetValues("X-BDTest-Report-Id").Single();
        var token = first.Headers.GetValues("X-BDTest-Attachment-Token").Single();
        using var noToken = await PostAttachment(server, firstId, attachmentId, null, new byte[] { 1, 2 });
        using var wrongReport = await PostAttachment(server, secondId, attachmentId, token, new byte[] { 1, 2 });
        using var unknown = await PostAttachment(server, firstId, Guid.NewGuid().ToString("N"), token, new byte[] { 1, 2 });
        using var shortBody = await PostAttachment(server, firstId, attachmentId, token, new byte[] { 1 });
        using var longBody = await PostAttachment(server, firstId, attachmentId, token, new byte[] { 1, 2, 3 });
        Assert.That(noToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(wrongReport.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(shortBody.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(longBody.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(Directory.GetFiles(files.Path, "*", SearchOption.AllDirectories), Is.Empty);
        using var missing = await server.Client.GetAsync($"bdtest/report/{firstId}/attachments/{attachmentId}");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var success = await PostAttachment(server, firstId, attachmentId, token, new byte[] { 1, 2 });
        Assert.That(success.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [TestCase("../outside", "text/plain", 2)]
    [TestCase("00112233445566778899aabbccddeeff", "text/plain\r\nX-Test:bad", 2)]
    [TestCase("00112233445566778899aabbccddeeff", "text/*", 2)]
    [TestCase("00112233445566778899aabbccddeeff", "text/plain", -1)]
    [TestCase("00112233445566778899aabbccddeeff", "text/plain", 10485761)]
    public async Task Invalid_attachment_manifest_is_rejected_before_storing_report(string id, string mime, long length)
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        using var response = await server.Post(AttachmentReport(id, mime, length));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(server.Store.Received, Is.Empty);
    }

    [Test]
    public async Task Deletion_removes_only_its_report_files_and_blocks_late_upload()
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        var attachmentId = Guid.NewGuid().ToString("N");
        using var first = await server.Post(AttachmentReport(attachmentId));
        using var second = await server.Post(AttachmentReport(attachmentId));
        var firstId = first.Headers.GetValues("X-BDTest-Report-Id").Single();
        var secondId = second.Headers.GetValues("X-BDTest-Report-Id").Single();
        var token = first.Headers.GetValues("X-BDTest-Attachment-Token").Single();
        using var a = await PostAttachment(server, firstId, attachmentId, token, new byte[] { 1, 2 });
        using var b = await PostAttachment(server, secondId, attachmentId,
            second.Headers.GetValues("X-BDTest-Attachment-Token").Single(), new byte[] { 3, 4 });
        await server.Repository.DeleteReport(firstId);
        using var missing = await files.Store.OpenReadAsync(firstId, attachmentId);
        using var remaining = await files.Store.OpenReadAsync(secondId, attachmentId);
        Assert.That(missing, Is.Null);
        Assert.That(remaining, Is.Not.Null);
        using var late = await PostAttachment(server, firstId, attachmentId, token, new byte[] { 1, 2 });
        Assert.That(late.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static string AttachmentReport(string id, string mime = "text/plain", long length = 2) =>
        ReportJson.SerializeObject(new { Scenarios = new[] { new { Attachments = new[] { new { Id = id, Name = "file.txt", MimeType = mime, Length = length, AttemptNumber = 1 } } } } });

    private static Task<HttpResponseMessage> PostAttachment(UploadServer server, string report, string id, string token, byte[] bytes)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"bdtest/data/{report}/attachments/{id}") { Content = new ByteArrayContent(bytes) };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        if (token != null) request.Headers.Add("X-BDTest-Attachment-Token", token);
        return Send();
        async Task<HttpResponseMessage> Send() { using (request) return await server.Client.SendAsync(request); }
    }

    private sealed class AttachmentFixture(BDTestRun run) : AbstractContextBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "upload-fixture";
    }

    private sealed class AttachmentHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private sealed class ObservingAttachmentStore(IBDTestAttachmentStore inner) : IBDTestAttachmentStore
    {
        public TaskCompletionSource<string> Deleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Stream> OpenReadAsync(string reportId, string attachmentId, CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(reportId, attachmentId, cancellationToken);
        public Task WriteAsync(string reportId, string attachmentId, Stream content, long length, CancellationToken cancellationToken = default)
            => inner.WriteAsync(reportId, attachmentId, content, length, cancellationToken);
        public async Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default)
        {
            await inner.DeleteReportAsync(reportId, cancellationToken);
            Deleted.TrySetResult(reportId);
        }
    }

    private sealed class BlockingAttachmentStore(IBDTestAttachmentStore inner) : IBDTestAttachmentStore
    {
        public string ReportToBlock { get; set; }
        public TaskCompletionSource Writing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Deleting { get; private set; }
        public Task<Stream> OpenReadAsync(string reportId, string attachmentId, CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(reportId, attachmentId, cancellationToken);
        public async Task WriteAsync(string reportId, string attachmentId, Stream content, long length, CancellationToken cancellationToken = default)
        {
            if (ReportToBlock == null || ReportToBlock == reportId)
            {
                Writing.TrySetResult();
                await Continue.Task.WaitAsync(cancellationToken);
            }
            await inner.WriteAsync(reportId, attachmentId, content, length, cancellationToken);
        }
        public Task DeleteReportAsync(string reportId, CancellationToken cancellationToken = default)
        {
            Deleting = true;
            return inner.DeleteReportAsync(reportId, cancellationToken);
        }
    }

    private sealed class BlockingAttachmentBody() : MemoryStream(new byte[] { 1, 2 })
    {
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reading.TrySetResult();
            await Continue.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class AttachmentDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BDTest-storage-tests", Guid.NewGuid().ToString("N"));
        public FileBDTestAttachmentStore Store { get; }
        public AttachmentDirectory() { Directory.CreateDirectory(Path); Store = new FileBDTestAttachmentStore(Path); }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
