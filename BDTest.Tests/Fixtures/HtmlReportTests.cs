using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class HtmlReportTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
    public void FinishExecution() => EndTest();

    [SetUp]
    public void SetUp() => TestResetHelper.ResetData();

    [Test]
    public void EmptyReportIsSelfContained()
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
        Assert.That(html, Does.Contain("<span class=\"label\">Total</span></span><span class=\"tile-n\">0</span>"));
        Assert.That(html, Does.Contain("No scenarios ran"));
        Assert.That(html, Does.Contain("<style>"));
        Assert.That(html, Does.Contain("<script>"));
        Assert.That(html, Does.Not.Contain("<script src="));
        Assert.That(html, Does.Not.Contain("<link "));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void ParentPolicyAllowsOnlyTheReportScript(string newline)
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport().Replace("\r\n", "\n").Replace("\n", newline);
        var policy = Regex.Match(html, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">");
        Assert.That(policy.Success, Is.True);
        Assert.That(policy.Index, Is.LessThan(html.IndexOf("<style>", StringComparison.Ordinal)));

        var directives = WebUtility.HtmlDecode(policy.Groups[1].Value).Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().Split(' ', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
        var scripts = Regex.Matches(html, "<script>(.*?)</script>", RegexOptions.Singleline);
        Assert.That(scripts.Count, Is.EqualTo(1));
        // HTML parsers normalize line endings before checking inline script hashes.
        var script = scripts[0].Groups[1].Value.Replace("\r\n", "\n").Replace("\r", "\n");
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        Assert.That(directives["script-src"], Is.EqualTo($"'sha256-{hash}'"));
        Assert.That(directives["script-src-attr"], Is.EqualTo("'none'"));
        Assert.That(directives["default-src"], Is.EqualTo("'none'"));
        Assert.That(directives["base-uri"], Is.EqualTo("'none'"));
        Assert.That(directives["form-action"], Is.EqualTo("'none'"));
        Assert.That(directives["frame-src"], Is.EqualTo("'self'"));
        Assert.That(directives["style-src"], Is.EqualTo("'unsafe-inline'"));
        Assert.That(directives["img-src"], Is.EqualTo("data: https: http: file:"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingDescriptorValuesUseConfiguredDefaults(bool nullDescriptor)
    {
        var environmentProperty = Required(typeof(BDTestSettings).GetProperty("Environment"));
        var tagProperty = Required(typeof(BDTestSettings).GetProperty("Tag"));
        var branchProperty = Required(typeof(BDTestSettings).GetProperty("BranchName"));
        var environment = environmentProperty.GetValue(null);
        var tag = tagProperty.GetValue(null);
        var branch = branchProperty.GetValue(null);
        try
        {
            environmentProperty.SetValue(null, "Configured environment");
            tagProperty.SetValue(null, "Configured tag");
            branchProperty.SetValue(null, "Configured branch");
            var html = nullDescriptor
                ? BDTestHtmlHelper.GetTestHtmlReport(null)
                : BDTestHtmlHelper.GetTestHtmlReport();

            Assert.That(html, Does.Contain("<dt>Environment</dt><dd>Configured environment</dd>"));
            Assert.That(html, Does.Contain("<dt>Tag</dt><dd>Configured tag</dd>"));
            Assert.That(html, Does.Contain("<dt>Branch</dt><dd class=\"mono\">Configured branch</dd>"));
        }
        finally
        {
            environmentProperty.SetValue(null, environment);
            tagProperty.SetValue(null, tag);
            branchProperty.SetValue(null, branch);
        }
    }

    [Test]
    public async Task DescriptorAndScenarioTextAreHtmlEncoded()
    {
        const string unsafeText = "<script>alert(\"x\" & 'y')</script>";
        const string encodedText = "&lt;script&gt;alert(&quot;x&quot; &amp; &#39;y&#39;)&lt;/script&gt;";
        var scenario = await When(() => DoNothing()).Then(() => DoNothing())
            .WithScenarioText(unsafeText).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor
        {
            Environment = unsafeText,
            Tag = "Explicit tag",
            BranchName = "Explicit branch"
        });

        Assert.That(html, Does.Not.Contain(unsafeText));
        Assert.That(html, Does.Contain($"<dt>Environment</dt><dd>{encodedText}</dd>"));
        Assert.That(html, Does.Contain($"class=\"scenario-title\">{encodedText}"));
        Assert.That(html, Does.Contain("<dt>Tag</dt><dd>Explicit tag</dd>"));
        Assert.That(html, Does.Contain("<dt>Branch</dt><dd class=\"mono\">Explicit branch</dd>"));
        Assert.That(html, Does.Contain($"id=\"details-{scenario.Guid}\""));
    }

    [Test]
    public async Task NotImplementedScenarioMatchesItsFilter()
    {
        await Assert.CatchAsync(async () => await When(() => NotImplemented()).Then(() => DoNothing()).BDTestAsync());
        Assert.That(BDTestUtil.GetScenarios().Single().Status, Is.EqualTo(Status.NotImplemented));
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var scenarioStatus = Regex.Match(html, "class=\"scn\" id=\"[^\"]+\" data-status=\"([^\"]+)\"").Groups[1].Value;
        var filterStatus = Regex.Match(html, "class=\"seg-btn\" type=\"button\" data-filter=\"([^\"]+)\" aria-pressed=\"false\"><span>Not implemented</span>").Groups[1].Value;

        Assert.That(filterStatus, Is.Not.Empty);
        Assert.That(filterStatus, Is.EqualTo(scenarioStatus));
        Assert.That(html, Does.Contain("<span class=\"label\">Not implemented</span></span><span class=\"tile-n\">1</span>"));
    }

    [Test]
    public async Task StepsAfterFailureAreNotReportedAsPassed()
    {
        await Assert.CatchAsync(async () => await When(() => Fail()).Then(() => DoNothing()).BDTestAsync());
        var scenario = BDTestUtil.GetScenarios().Single();
        Assert.That(scenario.Steps.Select(step => step.Status), Is.EqualTo(new[] { Status.Failed, Status.NotRun }));
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var stepStatuses = Regex.Matches(html, "class=\"step\" data-status=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToArray();

        Assert.That(stepStatuses, Is.EqualTo(new[] { "failed", "notrun" }));
        Assert.That(html, Does.Contain("&lt;failure&gt;"));
        Assert.That(html, Does.Not.Contain("<failure>"));
    }

    [Test]
    public async Task CustomHtmlIsIsolatedInAScriptlessFrame()
    {
        const string payload = "<script>parent.document.body.innerHTML = 'compromised';</script>";
        await When(() => WriteCustomHtml(payload)).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var frame = Regex.Match(html, "<iframe class=\"custom-body\"[^>]*>");

        Assert.That(frame.Success, Is.True);
        Assert.That(frame.Value, Does.Contain("sandbox=\"allow-popups\""));
        Assert.That(frame.Value, Does.Contain("Content-Security-Policy"));
        Assert.That(html, Does.Not.Contain(payload));
    }

    [TestCase("<script>parent.document.body.innerHTML = 'compromised';</script>")]
    [TestCase("<img src=x onerror=alert(document.cookie)>")]
    [TestCase("</div></section></main><div id=\"escaped\">outside</div>")]
    [TestCase("\"></iframe><script>alert('escaped')</script><iframe srcdoc=\"")]
    [TestCase("<p style=\"color: red\"><b>Formatted</b></p><table><tr><td>Result</td></tr></table><img src=\"evidence/screenshot.png\">")]
    public async Task CustomHtmlCannotEscapeOrChangeReportStructure(string payload)
    {
        var first = await When(() => WriteCustomHtml(payload)).Then(() => DoNothing()).WithScenarioText("First").BDTestAsync();
        var second = await When(() => DoNothing()).Then(() => DoNothing()).WithScenarioText("Second").BDTestAsync();
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var frames = Regex.Matches(html, "<iframe\\b[^>]*></iframe>");
        Assert.That(frames.Count, Is.EqualTo(1));
        var frame = XElement.Parse(frames[0].Value);
        var document = frame.Attribute("srcdoc")?.Value;
        Assert.That(document, Does.EndWith(payload + "</body></html>"));
        Assert.That(document, Does.Contain("default-src 'none'"));
        Assert.That(frame.Attribute("sandbox")?.Value, Is.EqualTo("allow-popups"));
        Assert.That(frame.Attribute("referrerpolicy")?.Value, Is.EqualTo("no-referrer"));

        var parent = html.Remove(frames[0].Index, frames[0].Length);
        Assert.That(parent, Does.Not.Contain(payload));
        Assert.That(Regex.Matches(parent, "<script>").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(parent, "<div class=\"scn\"").Count, Is.EqualTo(2));
        Assert.That(Regex.Matches(parent, "<main>").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(parent, "</main>").Count, Is.EqualTo(1));
        Assert.That(parent, Does.Contain($"id=\"details-{first.Guid}\""));
        Assert.That(parent, Does.Contain($"id=\"details-{second.Guid}\""));
        Assert.That(parent, Does.Contain("<span class=\"label\">Total</span></span><span class=\"tile-n\">2</span>"));
        Assert.That(Regex.IsMatch(parent, @"<[^>]+\son\w+\s*=", RegexOptions.IgnoreCase), Is.False);
    }

    [Test]
    public async Task RelativeEvidenceUrlsArePreservedInCustomOutput()
    {
        await When(() => WriteEvidence()).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var frame = Regex.Match(html, "<iframe class=\"custom-body\"[^>]*>");
        var document = WebUtility.HtmlDecode(Regex.Match(frame.Value, "srcdoc=\"([^\"]*)\"").Groups[1].Value);

        Assert.That(document, Does.Contain("href=\"evidence/report.html\" target=\"_blank\""));
        Assert.That(document, Does.Contain("src=\"evidence/screenshot.png\""));
        Assert.That(document, Does.Contain("img-src data: https: http: file:"));
        Assert.That(html, Does.Not.Contain("allow-scripts").And.Not.Contain("allow-popups-to-escape-sandbox"));
    }

    [Test]
    public async Task FailuresWithTheSameErrorAreGroupedIntoOneCause()
    {
        await Assert.CatchAsync(async () => await When(() => Fail()).Then(() => DoNothing()).WithScenarioText("First").BDTestAsync());
        await Assert.CatchAsync(async () => await When(() => Fail()).Then(() => DoNothing()).WithScenarioText("Second").BDTestAsync());

        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.Contain("Needs attention"));
        Assert.That(Regex.Matches(html, "<article class=\"cause\">").Count, Is.EqualTo(1));
        Assert.That(html, Does.Contain("<span class=\"cause-n\">2 scenarios</span>"));
        Assert.That(html, Does.Contain("<span class=\"v-word\">2 failed</span>"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OutlineFailureCausesCountDistinctScenariosAndRetainExampleLinks(bool includeOrdinaryScenario)
    {
        await Assert.CatchAsync(async () => await ScenarioOutline("Failing outline", new[] { 1, 2 },
            row => When(() => Fail()).Then(() => DoNothing())));
        var outline = BDTestUtil.GetScenarios().Single();
        if (includeOrdinaryScenario)
            await Assert.CatchAsync(async () => await When(() => Fail()).Then(() => DoNothing()).BDTestAsync());

        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var count = includeOrdinaryScenario ? 2 : 1;
        Assert.That(html, Does.Contain($"<span class=\"cause-n\">{count} {(count == 1 ? "scenario" : "scenarios")}</span>"));
        Assert.That(html, Does.Contain($"<span class=\"v-word\">{count} failed</span>"));
        Assert.That(html, Does.Contain($"data-go=\"{outline.Guid}\">Failing outline · example 1</button>"));
        Assert.That(html, Does.Contain($"data-go=\"{outline.Guid}\">Failing outline · example 2</button>"));
    }

    [Test]
    public async Task Aggregate_failure_shows_each_root_cause_and_its_encoded_tree()
    {
        var exception = new System.Reflection.TargetInvocationException(new AggregateException(
            new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>")));
        await Assert.CatchAsync(async () => await When(() => ThrowStructuredFailure(exception)).Then(() => DoNothing()).BDTestAsync());

        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(Regex.Matches(html, "<article class=\"cause\">").Count, Is.EqualTo(2));
        Assert.That(html, Does.Contain("<span class=\"err-type\">System.InvalidOperationException</span>"));
        Assert.That(html, Does.Contain("<span class=\"err-type\">System.ArgumentException</span>"));
        Assert.That(html, Does.Contain("<details class=\"exception-tree\" open>"));
        var originalText = Regex.Match(html, "<summary>Original exception text</summary>(.*?)</details>", RegexOptions.Singleline).Groups[1].Value;
        Assert.That(originalText, Does.Contain("data-wrap aria-pressed=\"false\">Wrap</button>"));
        Assert.That(originalText, Does.Contain("data-copy>Copy</button>"));
        Assert.That(html, Does.Contain("&lt;first cause&gt;").And.Contain("&lt;second cause&gt;"));
        Assert.That(html, Does.Not.Contain("<first cause>").And.Not.Contain("<second cause>"));
    }

    private static void ThrowStructuredFailure(Exception exception) => throw exception;

    [Test]
    [BDTestTag("api\tintegration\nline\rreturn\0null\bback\fform\\slash\"quote<>&'")]
    public async Task TagMetadataRoundTripsControlCharactersAndHtml()
    {
        var scenario = await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var attribute = Regex.Match(html, "data-tags=\"([^\"]*)\"").Groups[1].Value;

        Assert.That(attribute, Is.Not.Empty);
        var json = WebUtility.HtmlDecode(attribute);
        Assert.That(json.Any(char.IsControl), Is.False, "JSON must escape literal control characters.");
        Assert.That(ReportJson.DeserializeObject<string[]>(json), Is.EqualTo(scenario.Tags));
        var option = Regex.Match(html, "<option value=\"([^\"]+)\">@").Groups[1].Value;
        var button = Regex.Match(html, "data-tag=\"([^\"]+)\"").Groups[1].Value;
        foreach (var value in new[] { option, button })
        {
            Assert.That(value, Is.Not.Empty);
            Assert.That(value.Any(char.IsControl), Is.False, "Tag controls must not contain parser-normalized characters.");
            Assert.That(ReportJson.DeserializeObject<string>(WebUtility.HtmlDecode(value)), Is.EqualTo(scenario.Tags.Single()));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "The report must include an intentionally unexecuted scenario.")]
    public async Task UnexecutedDefinitionsAppearInSummaryAndPreventAllPassed(bool includePassingScenario)
    {
        if (includePassingScenario)
            await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();
        _ = When(() => DoNothing()).Then(() => DoNothing()).WithScenarioText("Unexecuted definition");

        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.Not.Contain("<span class=\"v-word\">All passed</span>"));
        var total = includePassingScenario ? 2 : 1;
        Assert.That(html, Does.Contain($"<span class=\"label\">Total</span></span><span class=\"tile-n\">{total}</span>"));
        var summary = Regex.Match(html, "<section class=\"wrap\" aria-label=\"Test summary\">.*?</section>", RegexOptions.Singleline).Value;
        Assert.That(summary, Does.Contain("href=\"#not-run\""));
        Assert.That(summary, Does.Contain("<span class=\"label\">Built but never run</span></span><span class=\"tile-n\">1</span>"));
        Assert.That(html, Does.Contain("Unexecuted definition"));
    }

    [Test]
    public async Task PassingRunHasNoAttentionSection()
    {
        await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.Not.Contain("Needs attention"));
        Assert.That(html, Does.Contain("<span class=\"v-word\">All passed</span>"));
    }

    [TestCase(0, "—")]
    [TestCase(412, "412 ms")]
    [TestCase(1840, "1.84 s")]
    [TestCase(252_000, "4 m 12 s")]
    [TestCase(3_720_000, "1 h 02 m")]
    public void DurationsAreReadable(int milliseconds, string expected)
    {
        Assert.That(BDTestHtmlHelper.FormatDuration(TimeSpan.FromMilliseconds(milliseconds)), Is.EqualTo(expected));
    }

    private void WriteCustomHtml(string html) => ScenarioHtmlWriter.Custom(html);
    private void WriteEvidence()
    {
        ScenarioHtmlWriter.Link("Supporting report", "evidence/report.html");
        ScenarioHtmlWriter.Image("evidence/screenshot.png");
    }
    private static void DoNothing() { }
    private static void NotImplemented() => throw new NotImplementedException("Not implemented yet");
    private static void Fail() => throw new InvalidOperationException("<failure>");
}
