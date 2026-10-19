using System.Xml.Linq;
using BDTest.Helpers;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class HtmlWriterTests : RunTestBase
{
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    [Before(HookType.Test)]
    public void BeginExecution() => BeginTest(_executionId);

    [After(HookType.Test)]
    public void CompleteTest() => EndTest();

    [Test]
    public async Task TextIsEncoded()
    {
        var html = await Render(writer => writer.Text("<script>\"A&B\" 'quoted'</script> &lt;"));
        await Assert.That(html).IsEqualTo("<span>&lt;script&gt;&quot;A&amp;B&quot; &#39;quoted&#39;&lt;/script&gt; &amp;lt;</span><br>");
    }

    [Test]
    [MatrixDataSource]
    public async Task LinkTextAndUrlAreEncoded([Matrix] bool useDelegate)
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
        await Assert.That(link.Name.LocalName).IsEqualTo("a");
        await Assert.That(link.Value).IsEqualTo(text);
        await Assert.That(link.Elements()).IsEmpty();
        await Assert.That(link.Attributes().Select(attribute => attribute.Name.LocalName)).IsEquivalentTo(new[] { "href", "target" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(link.Attribute("href")!.Value).IsEqualTo(url);
        await Assert.That(link.Attribute("target")!.Value).IsEqualTo("_blank");
        await Assert.That(calls).IsEqualTo(useDelegate ? 1 : 0);
    }

    [Test]
    public async Task ImageUrlIsEncoded()
    {
        const string url = "images/a\" onerror=\"alert(1)<>&'&quot;.png";
        var image = ParseElement(await Render(writer => writer.Image(url)));
        await Assert.That(image.Name.LocalName).IsEqualTo("img");
        await Assert.That(image.Attributes().Select(attribute => attribute.Name.LocalName)).IsEquivalentTo(new[] { "src", "loading" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(image.Attribute("src")!.Value).IsEqualTo(url);
        await Assert.That(image.Attribute("loading")!.Value).IsEqualTo("lazy");
    }

    public static IEnumerable<string> GetRejectedUrls() => RejectedUrls;

    public static IEnumerable<string> GetAllowedUrls() => AllowedUrls;

    private static readonly string[] AllowedUrls =
    {
        "https://example.com/image.png", "http://example.com/report", "HTTPS://example.com/",
        "//example.com/image.png", "/images/a.png", "images/a b.png", "./a.png", "../a.png",
        "#scenario", "?a=1&b=2", "/path/javascript:example", "?next=javascript:example",
        "#javascript:example", "./name:part.png", "images/é.png",
        "javascript%3Aalert(1)", "javascript&#58;alert(1)"
    };

    [Test]
    [MatrixDataSource]
    public async Task OrdinaryUrlsArePreserved(
        [MatrixMethod<HtmlWriterTests>(nameof(GetAllowedUrls))] string url, [Matrix("link", "delegate", "image")] string kind)
    {
        var element = ParseElement(await Render(writer => WriteUrl(writer, kind, url)));
        await Assert.That(element.Attribute(kind == "image" ? "src" : "href")!.Value).IsEqualTo(url);
    }

    [Test]
    [MatrixDataSource]
    public async Task MailtoIsAllowedForLinks([Matrix] bool useDelegate)
    {
        const string url = "mailto:team@example.com?subject=Report&body=Results";
        var element = ParseElement(await Render(writer => WriteUrl(writer, useDelegate ? "delegate" : "link", url)));
        await Assert.That(element.Attribute("href")!.Value).IsEqualTo(url);
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
    [MatrixDataSource]
    public async Task UnsafeUrlsAreRejectedWithoutWritingOutput(
        [MatrixMethod<HtmlWriterTests>(nameof(GetRejectedUrls))] string url, [Matrix("link", "delegate", "image")] string kind)
    {
        var scenario = await EmptyScenario();
        ScenarioHtmlWriter.Text("before");
        var exception = await Assert.That(() => WriteUrl(ScenarioHtmlWriter, kind, url)).ThrowsExactly<ArgumentException>();
        await Assert.That(exception!.ParamName).IsEqualTo("url");
        await Assert.That(scenario.CustomHtmlOutputForReport).IsEqualTo("<span>before</span><br>");
    }

    [Test]
    [MatrixDataSource]
    public async Task NullUrlsAreRejected([Matrix("link", "delegate", "image")] string kind)
    {
        var exception = await Assert.That(() => WriteUrl(ScenarioHtmlWriter, kind, null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(Required(exception).ParamName).IsEqualTo("url");
    }

    [Test]
    public async Task MailtoIsRejectedForImages()
    {
        await Assert.That(() => ScenarioHtmlWriter.Image("mailto:team@example.com")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task CustomAndLineBreakKeepTheirExplicitMarkup()
    {
        const string rawHtml = "<b>A &amp; B</b><script>alert(1)</script>";
        var html = await Render(writer => { writer.Custom(rawHtml); writer.LineBreak(); });
        await Assert.That(html).IsEqualTo(rawHtml + "<br>");
    }

    private async Task<string> Render(Action<HtmlWriter> write)
    {
        // Exercise writes buffered before scenario registration, as well as writes to an existing scenario above.
        write(ScenarioHtmlWriter);
        return Required((await EmptyScenario()).CustomHtmlOutputForReport);
    }

    private Task<Scenario> EmptyScenario() => When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
    private static void NoOp() { }
    private static XElement ParseElement(string html) => XElement.Parse(html[..^4]);

    private static void WriteUrl(HtmlWriter writer, string kind, string url)
    {
        switch (kind)
        {
            case "link": writer.Link("link", url); break;
            case "delegate": writer.Link(() => "link", url); break;
            case "image": writer.Image(url); break;
        }
    }
}
