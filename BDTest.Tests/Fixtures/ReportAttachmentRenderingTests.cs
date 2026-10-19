using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class ReportAttachmentRenderingTests
{
    private const string ReportId = "00112233445566778899aabbccddeeff";
    private const string AttachmentId = "112233445566778899aabbccddeeff00";

    [Test]
    [Arguments("image/png", true)]
    [Arguments("IMAGE/JPEG", true)]
    [Arguments("image/png; version=1", true)]
    [Arguments("IMAGE/JPEG; charset=binary", true)]
    [Arguments("image/png; profile=\"example\"", true)]
    [Arguments("image/svg+xml; charset=utf-8", false)]
    [Arguments("text/html; charset=utf-8", false)]
    [Arguments("image/png; profile=\"unterminated", false)]
    [Arguments("image/png, text/html", false)]
    [Arguments("", false)]
    [Arguments("image/gif", true)]
    [Arguments("image/webp", true)]
    [Arguments("image/bmp", true)]
    [Arguments("image/avif", true)]
    [Arguments("image/svg+xml", false)]
    [Arguments("text/html", false)]
    [Arguments("application/xhtml+xml", false)]
    [Arguments("application/pdf", false)]
    [Arguments("image/unknown", false)]
    [Arguments("text/plain", false)]
    [Arguments(null, false)]
    public async Task Only_known_raster_types_have_previews(string? mime, bool preview)
    {
        var html = await RenderAttachments(new Attachment { Id = AttachmentId, Name = "capture", MimeType = mime, AttemptNumber = 1 });
        await Assert.That(html.Contains("<img ")).IsEqualTo(preview);
        await Assert.That(html.Contains("data-bdtest-action=\"preview-attachment\"")).IsEqualTo(preview);
        await Assert.That(html).Contains($"href=\"/host/bdtest/report/{ReportId}/attachments/{AttachmentId}\"");
        await Assert.That(html).Contains("download");
        await Assert.That(html).DoesNotContain("<iframe");
        await Assert.That(html).DoesNotContain("<object");
        await Assert.That(html).DoesNotContain("<embed");
    }

    [Test]
    public async Task Metadata_is_encoded_and_untrusted_uri_is_never_used()
    {
        const string name = "<img onerror='alert(1)'> & \"photo\"";
        const string mime = "text/html\"><script>alert(2)</script>";
        var html = await RenderAttachments(new Attachment
        {
            Id = AttachmentId, Name = name, MimeType = mime, Length = 42, AttemptNumber = 2,
            Uri = "javascript:alert(3)"
        });
        await Assert.That(html).Contains(HtmlEncoder.Default.Encode(name));
        await Assert.That(html).Contains(HtmlEncoder.Default.Encode(mime));
        await Assert.That(html).DoesNotContain("<img");
        await Assert.That(html).DoesNotContain("<script");
        await Assert.That(html).DoesNotContain("javascript:");
        await Assert.That(html).Contains("42 bytes");
        await Assert.That(html).Contains("Attempt 2");
    }

    [Test]
    public async Task Unknown_identifiers_show_metadata_without_an_unsafe_link()
    {
        var html = await RenderAttachments(new Attachment { Id = "../bad", Name = "old capture", MimeType = "image/png", AttemptNumber = 1 });
        await Assert.That(html).Contains("old capture");
        await Assert.That(html).Contains("Content unavailable");
        await Assert.That(html).DoesNotContain("href=");
        await Assert.That(html).DoesNotContain("<img");
    }

    [Test]
    public async Task Outline_examples_and_panel_render_attachments_beneath_their_owners()
    {
        var scenario = ReportJson.DeserializeObject<Scenario>("""
            {"Guid":"outline","IsOutline":true,"Attachments":[
                {"Id":"00112233445566778899aabbccddeeff","Name":"outline setup","AttemptNumber":1}],
             "Examples":[{"Guid":"row","Attachments":[
                {"Id":"112233445566778899aabbccddeeff00","Name":"row setup","AttemptNumber":1}],
                "Steps":[{"StepText":"When row runs","Attachments":[
                    {"Id":"2233445566778899aabbccddeeff0011","Name":"row step","AttemptNumber":2}]}]}]}
            """);
        foreach (var (path, model) in new (string, object)[]
        {
            ("/Views/BDTest/Scenario.cshtml", scenario),
            ("/Views/Shared/_ScenarioPanel.cshtml", new ScenarioModalViewModel { Scenario = (scenario).ToResult() })
        })
        {
            var html = await Render(path, model);
            await Assert.That(html).Contains("outline setup");
            await Assert.That(html).Contains("row setup");
            await Assert.That(html).Contains("row step");
            var row = Regex.Match(html, "<tr class=\"ex-detail\".*?</tr>", RegexOptions.Singleline).Value;
            await Assert.That(row).Contains("row setup");
            await Assert.That(row).Contains("row step");
            await Assert.That(row).DoesNotContain("outline setup");
            await Assert.That(row.IndexOf("When", StringComparison.Ordinal)).IsLessThan(row.IndexOf("row step", StringComparison.Ordinal));
            await Assert.That(row).Contains("Attempt 2");
        }
    }

    [Test]
    [Arguments("{}")]
    [Arguments("{\"Attachments\":null,\"Steps\":[{\"Attachments\":null}]}")]
    public async Task Historical_reports_without_attachments_still_render(string json)
    {
        var html = await Render("/Views/BDTest/Scenario.cshtml", ReportJson.DeserializeObject<Scenario>(json));
        await Assert.That(html).Contains("Steps");
        await Assert.That(html).DoesNotContain("class=\"attachment-list\"");
    }

    private static Task<string> RenderAttachments(params Attachment[] attachments) => Render("/Views/Shared/_Attachments.cshtml", attachments);

    private static Task<string> Render(string path, object model) => ReportHandlerEncodingTests.RenderAsync(path, model,
        pathBase: "/host", configureViewData: data => data[nameof(ReportLayoutViewModel)] = ReportLayoutViewModel.Create(
            (ReportJson.DeserializeObject<BDTest.Maps.BDTestOutputModel>($"{{\"Id\":\"{ReportId}\"}}")).ToReport()));
}
