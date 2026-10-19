using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportAttachmentRenderingTests
{
    private const string ReportId = "00112233445566778899aabbccddeeff";
    private const string AttachmentId = "112233445566778899aabbccddeeff00";

    [TestCase("image/png", true)]
    [TestCase("IMAGE/JPEG", true)]
    [TestCase("image/gif", true)]
    [TestCase("image/webp", true)]
    [TestCase("image/bmp", true)]
    [TestCase("image/avif", true)]
    [TestCase("image/svg+xml", false)]
    [TestCase("text/html", false)]
    [TestCase("application/xhtml+xml", false)]
    [TestCase("application/pdf", false)]
    [TestCase("image/unknown", false)]
    [TestCase("text/plain", false)]
    [TestCase(null, false)]
    public async Task Only_known_raster_types_have_previews(string? mime, bool preview)
    {
        var html = await RenderAttachments(new Attachment { Id = AttachmentId, Name = "capture", MimeType = mime, AttemptNumber = 1 });
        Assert.That(html.Contains("<img "), Is.EqualTo(preview));
        Assert.That(html.Contains("data-bdtest-action=\"preview-attachment\""), Is.EqualTo(preview));
        Assert.That(html, Does.Contain($"href=\"/host/bdtest/report/{ReportId}/attachments/{AttachmentId}\""));
        Assert.That(html, Does.Contain("download"));
        Assert.That(html, Does.Not.Contain("<iframe").And.Not.Contain("<object").And.Not.Contain("<embed"));
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
        Assert.That(html, Does.Contain(HtmlEncoder.Default.Encode(name)));
        Assert.That(html, Does.Contain(HtmlEncoder.Default.Encode(mime)));
        Assert.That(html, Does.Not.Contain("<img").And.Not.Contain("<script").And.Not.Contain("javascript:"));
        Assert.That(html, Does.Contain("42 bytes").And.Contain("Attempt 2"));
    }

    [Test]
    public async Task Unknown_identifiers_show_metadata_without_an_unsafe_link()
    {
        var html = await RenderAttachments(new Attachment { Id = "../bad", Name = "old capture", MimeType = "image/png", AttemptNumber = 1 });
        Assert.That(html, Does.Contain("old capture").And.Contain("Content unavailable"));
        Assert.That(html, Does.Not.Contain("href=").And.Not.Contain("<img"));
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
            ("/Views/Shared/_ScenarioPanel.cshtml", new ScenarioModalViewModel { Scenario = scenario })
        })
        {
            var html = await Render(path, model);
            Assert.That(html, Does.Contain("outline setup").And.Contain("row setup").And.Contain("row step"));
            var row = Regex.Match(html, "<details class=\"example-row\".*?</details>", RegexOptions.Singleline).Value;
            Assert.That(row, Does.Contain("row setup").And.Contain("row step").And.Not.Contain("outline setup"));
            Assert.That(row.IndexOf("When", StringComparison.Ordinal), Is.LessThan(row.IndexOf("row step", StringComparison.Ordinal)));
            Assert.That(row, Does.Contain("Attempt 2"));
        }
    }

    [TestCase("{}")]
    [TestCase("{\"Attachments\":null,\"Steps\":[{\"Attachments\":null}]}")]
    public async Task Historical_reports_without_attachments_still_render(string json)
    {
        var html = await Render("/Views/BDTest/Scenario.cshtml", ReportJson.DeserializeObject<Scenario>(json));
        Assert.That(html, Does.Contain("Steps").And.Not.Contain("class=\"attachment-list\""));
    }

    private static Task<string> RenderAttachments(params Attachment[] attachments) => Render("/Views/Shared/_Attachments.cshtml", attachments);

    private static Task<string> Render(string path, object model) => ReportHandlerEncodingTests.RenderAsync(path, model,
        pathBase: "/host", configureViewData: data => data[nameof(ReportLayoutViewModel)] = new ReportLayoutViewModel
        {
            Report = ReportJson.DeserializeObject<BDTest.Maps.BDTestOutputModel>($"{{\"Id\":\"{ReportId}\"}}")
        });
}
