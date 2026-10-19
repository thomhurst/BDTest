using System.Net;
using System.Text;
using System.Text.Json;
using BDTest.Contracts;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace BDTest.Tests.Fixtures;

public partial class ReportUploadTests
{
    [Test]
    [MatrixDataSource]
    public async Task Upload_protocol_is_independent_of_Accept_and_host_JSON_options(
        [Matrix(false, true)] bool typed,
        [Matrix("application/json", "text/plain", "application/xml")] string accept)
    {
        await using var server = await UploadServer.Start(configureJson: options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
            options.WriteIndented = true;
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "bdtest/data")
        {
            Content = new StringContent("{\"Tag\":\"protocol\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-BDTest-Upload-Key", Key);
        request.Headers.Accept.ParseAdd(accept);
        if (typed) request.Headers.Add("X-BDTest-Upload-Protocol", "1");
        using var response = await server.Client.SendAsync(request);
        var id = response.Headers.GetValues("X-BDTest-Report-Id").Single();
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(server.Store.Received.Single().Id).IsEqualTo(id);
        if (typed)
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
            await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("application/json");
            var result = JsonSerializer.Deserialize(body, BDTestReportJsonContext.Default.BDTestReportUploadResult)!;
            await Assert.That(result.ReportId).IsEqualTo(id);
            await Assert.That(result.ReportUrl).IsEqualTo(response.Headers.Location);
            await Assert.That(result.ReportUrl.AbsolutePath).IsEqualTo($"/bdtest/report/{id}/summary");
            using var json = JsonDocument.Parse(body);
            await Assert.That(json.RootElement.EnumerateObject().Select(property => property.Name)).IsEquivalentTo(new[] { "reportUrl", "reportId" });
        }
        else
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
            await Assert.That(new Uri(body).AbsolutePath).IsEqualTo($"/bdtest/report/{id}/summary");
        }
    }

    [Test]
    [Arguments("application/xml")]
    [Arguments("text/plain")]
    public async Task Typed_uploader_round_trips_current_schema_and_authorized_attachments(string accept)
    {
        using var files = new AttachmentDirectory();
        await using var server = await UploadServer.Start(configureServices: services => services.AddSingleton<IBDTestAttachmentStore>(files.Store));
        server.Client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var run = BDTestRun.Start();
        var fixture = new AttachmentFixture(run);
        var scenario = await fixture.When(() => fixture.Attachments.Attach("log.txt", new byte[] { 1, 2, 3 }, "text/plain"))
            .Then(() => { }).BDTestAsync();
        var uploader = new BDTestReportUploader(server.Client, new(server.Client.BaseAddress!) { UploadKey = Key });
        var result = await uploader.UploadReportAsync(run);
        var stored = server.Store.Received.Single();
        await Assert.That(stored.SchemaVersion).IsEqualTo(1);
        await Assert.That(result.ReportId).IsEqualTo(stored.Id);
        await Assert.That(result.ReportId).IsNotEqualTo(run.ReportId);
        await Assert.That(result.ReportUrl.AbsolutePath).Contains(stored.Id);
        var attachment = scenario.Steps![0].Attachments!.Single();
        using var bytes = await files.Store.OpenReadAsync(stored.Id, attachment.Id!);
        await Assert.That(bytes).IsNotNull();
        await Assert.That(bytes!.ReadByte()).IsEqualTo(1);
        using var retained = run.OpenAttachment(attachment.Id!);
        await Assert.That(retained.Length).IsEqualTo(3L);
        await Assert.That(server.Client.DefaultRequestHeaders.Accept.Single().MediaType).IsEqualTo(accept);
        await AssertReportPagesRender(server, stored.Id);
    }

    [Test]
    public async Task All_static_signatures_are_obsolete_forwarders()
    {
        var methods = typeof(BDTestReportServer).GetMethods().Where(method => method.Name == "SendDataAndGetReportUriAsync").ToArray();
        await Assert.That(methods.Length).IsEqualTo(4);
        foreach (var method in methods)
            await Assert.That(Attribute.GetCustomAttribute(method, typeof(ObsoleteAttribute))).IsNotNull();
        // The owned-client overload without a token also remains callable, reaching the same endpoint.
        using var run = BDTestRun.Start();
        await using var server = await UploadServer.Start(configureApp: app => app.Use((context, next) =>
        {
            context.Request.Headers["X-BDTest-Upload-Key"] = Key;
            return next(context);
        }));
        var uri = await CompatibilityReportUpload.SendDataAndGetReportUriAsync(run, server.Client.BaseAddress!, null);
        await Assert.That(uri.AbsolutePath).Contains(server.Store.Received.Single().Id);
    }
}
