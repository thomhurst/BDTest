using System.Xml.Linq;
using BDTest.Helpers;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class HtmlWriterTests : BDTestBase
{
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    [SetUp]
    public void BeginExecution() => BeginTest(_executionId);

    [TearDown]
    public void CompleteTest() => EndTest();

    [Test]
    public async Task TextIsEncoded()
    {
        var html = await Render(writer => writer.Text("<script>\"A&B\" 'quoted'</script> &lt;"));
        Assert.That(html, Is.EqualTo("<span>&lt;script&gt;&quot;A&amp;B&quot; &#39;quoted&#39;&lt;/script&gt; &amp;lt;</span><br>"));
    }

    [Test]
    public async Task LinkTextAndUrlAreEncoded([Values] bool useDelegate)
    {
        const string text = "<b>\"A&B\" 'quoted'</b> &lt;";
        const string url = "https://example.com/?a=\"><img src=x onerror=alert(1)>&b='quoted'&c=&quot;";
        var calls = 0;
        var html = await Render(writer =>
        {
            if (useDelegate)
            {
                writer.Link(() => { calls++; return text; }, url);
            }
            else
            {
                writer.Link(text, url);
            }
        });
        var link = ParseElement(html);
        Assert.That(link.Name.LocalName, Is.EqualTo("a"));
        Assert.That(link.Value, Is.EqualTo(text));
        Assert.That(link.Elements(), Is.Empty);
        Assert.That(link.Attributes().Select(attribute => attribute.Name.LocalName), Is.EqualTo(new[] { "href", "target" }));
        Assert.That(link.Attribute("href")!.Value, Is.EqualTo(url));
        Assert.That(link.Attribute("target")!.Value, Is.EqualTo("_blank"));
        Assert.That(calls, Is.EqualTo(useDelegate ? 1 : 0));
    }

    [Test]
    public async Task ImageUrlIsEncoded()
    {
        const string url = "images/a\" onerror=\"alert(1)<>&'&quot;.png";
        var image = ParseElement(await Render(writer => writer.Image(url)));
        Assert.That(image.Name.LocalName, Is.EqualTo("img"));
        Assert.That(image.Attributes().Select(attribute => attribute.Name.LocalName), Is.EqualTo(new[] { "src", "loading" }));
        Assert.That(image.Attribute("src")!.Value, Is.EqualTo(url));
        Assert.That(image.Attribute("loading")!.Value, Is.EqualTo("lazy"));
    }

    private static readonly string[] AllowedUrls =
    {
        "https://example.com/image.png", "http://example.com/report", "HTTPS://example.com/",
        "//example.com/image.png", "/images/a.png", "images/a b.png", "./a.png", "../a.png",
        "#scenario", "?a=1&b=2", "/path/javascript:example", "?next=javascript:example",
        "#javascript:example", "./name:part.png", "images/é.png",
        "javascript%3Aalert(1)", "javascript&#58;alert(1)"
    };

    [Test]
    public async Task OrdinaryUrlsArePreserved(
        [ValueSource(nameof(AllowedUrls))] string url, [Values("link", "delegate", "image")] string kind)
    {
        var element = ParseElement(await Render(writer => WriteUrl(writer, kind, url)));
        Assert.That(element.Attribute(kind == "image" ? "src" : "href")!.Value, Is.EqualTo(url));
    }

    [Test]
    public async Task MailtoIsAllowedForLinks([Values] bool useDelegate)
    {
        const string url = "mailto:team@example.com?subject=Report&body=Results";
        var element = ParseElement(await Render(writer => WriteUrl(writer, useDelegate ? "delegate" : "link", url)));
        Assert.That(element.Attribute("href")!.Value, Is.EqualTo(url));
    }

    private static readonly string[] RejectedUrls =
    {
        "javascript:alert(1)", "JaVaScRiPt:alert(1)", "vbscript:msgbox(1)",
        "data:text/html,<script>alert(1)</script>", "data:image/svg+xml,<svg onload='alert(1)'/>",
        "data:image/png;base64,AAAA", "file:///C:/secret", "ftp://example.com/file", "blob:https://example.com/id",
        "custom:report", " javascript:alert(1)", "\tjavascript:alert(1)", "java\nscript:alert(1)",
        "java\rscript:alert(1)", "java\tscript:alert(1)", "\0javascript:alert(1)",
        "https://example.com/\nimage.png", "https://example.com/ ", "C:\\image.png", "\\\\server\\image.png", "", " "
    };

    [Test]
    public async Task UnsafeUrlsAreRejectedWithoutWritingOutput(
        [ValueSource(nameof(RejectedUrls))] string url, [Values("link", "delegate", "image")] string kind)
    {
        var scenario = await EmptyScenario();
        ScenarioHtmlWriter.Text("before");
        var exception = Assert.Throws<ArgumentException>(() => WriteUrl(ScenarioHtmlWriter, kind, url));
        Assert.That(exception!.ParamName, Is.EqualTo("url"));
        Assert.That(scenario.CustomHtmlOutputForReport, Is.EqualTo("<span>before</span><br>"));
    }

    [Test]
    public void NullUrlsAreRejected([Values("link", "delegate", "image")] string kind)
    {
        Assert.That(Assert.Throws<ArgumentNullException>(() => WriteUrl(ScenarioHtmlWriter, kind, null))!.ParamName,
            Is.EqualTo("url"));
    }

    [Test]
    public void MailtoIsRejectedForImages()
    {
        Assert.Throws<ArgumentException>(() => ScenarioHtmlWriter.Image("mailto:team@example.com"));
    }

    [Test]
    public async Task CustomAndLineBreakKeepTheirExplicitMarkup()
    {
        const string rawHtml = "<b>A &amp; B</b><script>alert(1)</script>";
        var html = await Render(writer => { writer.Custom(rawHtml); writer.LineBreak(); });
        Assert.That(html, Is.EqualTo(rawHtml + "<br>"));
    }

    private async Task<string> Render(Action<HtmlWriter> write)
    {
        // Exercise writes buffered before scenario registration, as well as writes to an existing scenario above.
        write(ScenarioHtmlWriter);
        return (await EmptyScenario()).CustomHtmlOutputForReport;
    }

    private Task<Scenario> EmptyScenario() => When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
    private static void NoOp() { }
    private static XElement ParseElement(string html) => XElement.Parse(html[..^4]);

    private static void WriteUrl(HtmlWriter writer, string kind, string? url)
    {
        switch (kind)
        {
            case "link": writer.Link("link", url); break;
            case "delegate": writer.Link(() => "link", url); break;
            case "image": writer.Image(url); break;
        }
    }
}
