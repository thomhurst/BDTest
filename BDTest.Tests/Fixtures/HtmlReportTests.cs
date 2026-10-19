using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class HtmlReportTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    [Test]
    public async Task EmptyReportIsSelfContained()
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(html).StartsWith("<!DOCTYPE html>");
        await Assert.That(html).Contains("<span class=\"label\">Total</span></span><span class=\"tile-n\">0</span>");
        await Assert.That(html).Contains("No scenarios ran");
        await Assert.That(html).Contains("<style>");
        await Assert.That(html).Contains("<script>");
        await Assert.That(html).DoesNotContain("<script src=");
        await Assert.That(html).DoesNotContain("<link ");
    }

    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task ParentPolicyAllowsOnlyTheReportScript(string newline)
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun).Replace("\r\n", "\n").Replace("\n", newline);
        var policy = Regex.Match(html, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">");
        await Assert.That(policy.Success).IsTrue();
        await Assert.That(policy.Index).IsLessThan(html.IndexOf("<style>", StringComparison.Ordinal));

        var directives = WebUtility.HtmlDecode(policy.Groups[1].Value).Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().Split(' ', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
        var scripts = Regex.Matches(html, "<script>(.*?)</script>", RegexOptions.Singleline);
        await Assert.That(scripts.Count).IsEqualTo(1);
        // HTML parsers normalize line endings before checking inline script hashes.
        var script = scripts[0].Groups[1].Value.Replace("\r\n", "\n").Replace("\r", "\n");
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        await Assert.That(directives["script-src"]).IsEqualTo($"'sha256-{hash}'");
        await Assert.That(directives["script-src-attr"]).IsEqualTo("'none'");
        await Assert.That(directives["default-src"]).IsEqualTo("'none'");
        await Assert.That(directives["base-uri"]).IsEqualTo("'none'");
        await Assert.That(directives["form-action"]).IsEqualTo("'none'");
        await Assert.That(directives["frame-src"]).IsEqualTo("'self'");
        await Assert.That(directives["style-src"]).IsEqualTo("'unsafe-inline'");
        await Assert.That(directives["img-src"]).IsEqualTo("data: https: http: file:");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingDescriptorValuesAreNotSpecified(bool nullDescriptor)
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport(nullDescriptor ? null : new BDTestRunDescriptor(), TestRun);
        await Assert.That(html).Contains("Not Specified");
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
        }, TestRun);

        await Assert.That(html).DoesNotContain(unsafeText);
        await Assert.That(html).Contains($"<dt>Environment</dt><dd>{encodedText}</dd>");
        await Assert.That(html).Contains($"class=\"scenario-title\">{encodedText}");
        await Assert.That(html).Contains("<dt>Tag</dt><dd>Explicit tag</dd>");
        await Assert.That(html).Contains("<dt>Branch</dt><dd class=\"mono\">Explicit branch</dd>");
        await Assert.That(html).Contains($"id=\"details-{scenario.Guid}\"");
    }

    [Test]
    public async Task NotImplementedScenarioMatchesItsFilter()
    {
        await Assert.That(async () => await When(() => NotImplemented()).Then(() => DoNothing()).BDTestAsync()).Throws<Exception>();
        await Assert.That(BDTestUtil.GetScenarios(TestRun).Single().Status).IsEqualTo(Status.NotImplemented);
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var scenarioStatus = Regex.Match(html, "class=\"scn\" id=\"[^\"]+\" data-status=\"([^\"]+)\"").Groups[1].Value;
        var filterStatus = Regex.Match(html, "class=\"seg-btn\" type=\"button\" data-filter=\"([^\"]+)\" aria-pressed=\"false\"><span>Not implemented</span>").Groups[1].Value;

        await Assert.That(filterStatus).IsNotEmpty();
        await Assert.That(filterStatus).IsEqualTo(scenarioStatus);
        await Assert.That(html).Contains("<span class=\"label\">Not implemented</span></span><span class=\"tile-n\">1</span>");
    }

    [Test]
    public async Task StepsAfterFailureAreNotReportedAsPassed()
    {
        await Assert.That(async () => await When(() => Fail()).Then(() => DoNothing()).BDTestAsync()).Throws<Exception>();
        var scenario = BDTestUtil.GetScenarios(TestRun).Single();
        await Assert.That(Required(scenario.Steps).Select(step => step.Status)).IsEquivalentTo(new[] { Status.Failed, Status.NotRun }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var stepStatuses = Regex.Matches(html, "class=\"step\" data-status=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToArray();

        await Assert.That(stepStatuses).IsEquivalentTo(new[] { "failed", "notrun" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(html).Contains("&lt;failure&gt;");
        await Assert.That(html).DoesNotContain("<failure>");
    }

    [Test]
    public async Task CustomHtmlIsIsolatedInAScriptlessFrame()
    {
        const string payload = "<script>parent.document.body.innerHTML = 'compromised';</script>";
        await When(() => WriteCustomHtml(payload)).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var frame = Regex.Match(html, "<iframe class=\"custom-body\"[^>]*>");

        await Assert.That(frame.Success).IsTrue();
        await Assert.That(frame.Value).Contains("sandbox=\"allow-popups\"");
        await Assert.That(frame.Value).Contains("Content-Security-Policy");
        await Assert.That(html).DoesNotContain(payload);
    }

    [Test]
    [Arguments("<script>parent.document.body.innerHTML = 'compromised';</script>")]
    [Arguments("<img src=x onerror=alert(document.cookie)>")]
    [Arguments("</div></section></main><div id=\"escaped\">outside</div>")]
    [Arguments("\"></iframe><script>alert('escaped')</script><iframe srcdoc=\"")]
    [Arguments("<p style=\"color: red\"><b>Formatted</b></p><table><tr><td>Result</td></tr></table><img src=\"evidence/screenshot.png\">")]
    public async Task CustomHtmlCannotEscapeOrChangeReportStructure(string payload)
    {
        var first = await When(() => WriteCustomHtml(payload)).Then(() => DoNothing()).WithScenarioText("First").BDTestAsync();
        var second = await When(() => DoNothing()).Then(() => DoNothing()).WithScenarioText("Second").BDTestAsync();
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var frames = Regex.Matches(html, "<iframe\\b[^>]*></iframe>");
        await Assert.That(frames.Count).IsEqualTo(1);
        var frame = XElement.Parse(frames[0].Value);
        var document = frame.Attribute("srcdoc")?.Value;
        await Assert.That(document).EndsWith(payload + "</body></html>");
        await Assert.That(document).Contains("default-src 'none'");
        await Assert.That(frame.Attribute("sandbox")?.Value).IsEqualTo("allow-popups");
        await Assert.That(frame.Attribute("referrerpolicy")?.Value).IsEqualTo("no-referrer");

        var parent = html.Remove(frames[0].Index, frames[0].Length);
        await Assert.That(parent).DoesNotContain(payload);
        await Assert.That(Regex.Matches(parent, "<script>").Count).IsEqualTo(1);
        await Assert.That(Regex.Matches(parent, "<div class=\"scn\"").Count).IsEqualTo(2);
        await Assert.That(Regex.Matches(parent, "<main>").Count).IsEqualTo(1);
        await Assert.That(Regex.Matches(parent, "</main>").Count).IsEqualTo(1);
        await Assert.That(parent).Contains($"id=\"details-{first.Guid}\"");
        await Assert.That(parent).Contains($"id=\"details-{second.Guid}\"");
        await Assert.That(parent).Contains("<span class=\"label\">Total</span></span><span class=\"tile-n\">2</span>");
        await Assert.That(Regex.IsMatch(parent, @"<[^>]+\son\w+\s*=", RegexOptions.IgnoreCase)).IsFalse();
    }

    [Test]
    public async Task RelativeEvidenceUrlsArePreservedInCustomOutput()
    {
        await When(() => WriteEvidence()).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var frame = Regex.Match(html, "<iframe class=\"custom-body\"[^>]*>");
        var document = WebUtility.HtmlDecode(Regex.Match(frame.Value, "srcdoc=\"([^\"]*)\"").Groups[1].Value);

        await Assert.That(document).Contains("href=\"evidence/report.html\" target=\"_blank\"");
        await Assert.That(document).Contains("src=\"evidence/screenshot.png\"");
        await Assert.That(document).Contains("img-src data: https: http: file:");

        await Assert.That(html).DoesNotContain("allow-scripts").And.DoesNotContain("allow-popups-to-escape-sandbox");
    }

    [Test]
    public async Task FailuresWithTheSameErrorAreGroupedIntoOneCause()
    {
        await Assert.That(async () => await When(() => Fail()).Then(() => DoNothing()).WithScenarioText("First").BDTestAsync()).Throws<Exception>();
        await Assert.That(async () => await When(() => Fail()).Then(() => DoNothing()).WithScenarioText("Second").BDTestAsync()).Throws<Exception>();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(html).Contains("Needs attention");
        await Assert.That(Regex.Matches(html, "<article class=\"cause\">").Count).IsEqualTo(1);
        await Assert.That(html).Contains("<span class=\"cause-n\">2 scenarios</span>");
        await Assert.That(html).Contains("<span class=\"v-word\">2 failed</span>");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OutlineFailureCausesCountDistinctScenariosAndRetainExampleLinks(bool includeOrdinaryScenario)
    {
        await Assert.That(async () => await ScenarioOutline("Failing outline", new[] { 1, 2 },
            row => When(() => Fail()).Then(() => DoNothing()))).Throws<Exception>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single();
        if (includeOrdinaryScenario)
            await Assert.That(async () => await When(() => Fail()).Then(() => DoNothing()).BDTestAsync()).Throws<Exception>();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var count = includeOrdinaryScenario ? 2 : 1;
        await Assert.That(html).Contains($"<span class=\"cause-n\">{count} {(count == 1 ? "scenario" : "scenarios")}</span>");
        await Assert.That(html).Contains($"<span class=\"v-word\">{count} failed</span>");
        await Assert.That(html).Contains($"data-go=\"{outline.Guid}\">Failing outline · example 1</button>");
        await Assert.That(html).Contains($"data-go=\"{outline.Guid}\">Failing outline · example 2</button>");
    }

    [Test]
    public async Task Aggregate_failure_shows_each_root_cause_and_its_encoded_tree()
    {
        var exception = new System.Reflection.TargetInvocationException(new AggregateException(
            new InvalidOperationException("<first cause>"), new ArgumentException("<second cause>")));
        await Assert.That(async () => await When(() => ThrowStructuredFailure(exception)).Then(() => DoNothing()).BDTestAsync()).Throws<Exception>();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(Regex.Matches(html, "<article class=\"cause\">").Count).IsEqualTo(2);
        await Assert.That(html).Contains("<span class=\"err-type\">System.InvalidOperationException</span>");
        await Assert.That(html).Contains("<span class=\"err-type\">System.ArgumentException</span>");
        await Assert.That(html).Contains("<details class=\"exception-tree\" open>");
        var originalText = Regex.Match(html, "<summary>Original exception text</summary>(.*?)</details>", RegexOptions.Singleline).Groups[1].Value;
        await Assert.That(originalText).Contains("data-wrap aria-pressed=\"false\">Wrap</button>");
        await Assert.That(originalText).Contains("data-copy>Copy</button>");

        await Assert.That(html).Contains("&lt;first cause&gt;").And.Contains("&lt;second cause&gt;");

        await Assert.That(html).DoesNotContain("<first cause>").And.DoesNotContain("<second cause>");
    }

    private static void ThrowStructuredFailure(Exception exception) => throw exception;

    [Test]
    [BDTestTag("api\tintegration\nline\rreturn\0null\bback\fform\\slash\"quote<>&'")]
    public async Task TagMetadataRoundTripsControlCharactersAndHtml()
    {
        var scenario = await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var attribute = Regex.Match(html, "data-tags=\"([^\"]*)\"").Groups[1].Value;

        await Assert.That(attribute).IsNotEmpty();
        var json = WebUtility.HtmlDecode(attribute);
        await Assert.That(json.Any(char.IsControl)).IsFalse().Because("JSON must escape literal control characters.");
        await Assert.That(ReportJson.DeserializeObject<string[]>(json)).IsEquivalentTo(Required(scenario.Tags), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var option = Regex.Match(html, "<option value=\"([^\"]+)\">@").Groups[1].Value;
        var button = Regex.Match(html, "data-tag=\"([^\"]+)\"").Groups[1].Value;
        foreach (var value in new[] { option, button })
        {
            await Assert.That(value).IsNotEmpty();
            await Assert.That(value.Any(char.IsControl)).IsFalse().Because("Tag controls must not contain parser-normalized characters.");
            await Assert.That(ReportJson.DeserializeObject<string>(WebUtility.HtmlDecode(value))).IsEqualTo(scenario.Tags.Single());
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "The report must include an intentionally unexecuted scenario.")]
    public async Task UnexecutedDefinitionsAppearInSummaryAndPreventAllPassed(bool includePassingScenario)
    {
        if (includePassingScenario)
            await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();
        _ = When(() => DoNothing()).Then(() => DoNothing()).WithScenarioText("Unexecuted definition");

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(html).DoesNotContain("<span class=\"v-word\">All passed</span>");
        var total = includePassingScenario ? 2 : 1;
        await Assert.That(html).Contains($"<span class=\"label\">Total</span></span><span class=\"tile-n\">{total}</span>");
        var summary = Regex.Match(html, "<section class=\"wrap\" aria-label=\"Test summary\">.*?</section>", RegexOptions.Singleline).Value;
        await Assert.That(summary).Contains("href=\"#not-run\"");
        await Assert.That(summary).Contains("<span class=\"label\">Built but never run</span></span><span class=\"tile-n\">1</span>");
        await Assert.That(html).Contains("Unexecuted definition");
    }

    [Test]
    public async Task PassingRunHasNoAttentionSection()
    {
        await When(() => DoNothing()).Then(() => DoNothing()).BDTestAsync();

        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(html).DoesNotContain("Needs attention");
        await Assert.That(html).Contains("<span class=\"v-word\">All passed</span>");
    }

    [Test]
    [Arguments(0, "—")]
    [Arguments(412, "412 ms")]
    [Arguments(1840, "1.84 s")]
    [Arguments(252_000, "4 m 12 s")]
    [Arguments(3_720_000, "1 h 02 m")]
    public async Task DurationsAreReadable(int milliseconds, string expected)
    {
        var scenario = new BDTest.Contracts.ScenarioResult("scenario", "report", "story", "scenario",
            BDTest.Contracts.Status.Passed, timeTaken: TimeSpan.FromMilliseconds(milliseconds));
        var html = BDTestHtmlHelper.GenerateHtmlReport(new BDTest.Contracts.BDTestReport(1, "report", "tests", [scenario]));
        await Assert.That(html).Contains($"<div><dt>Duration</dt><dd class=\"mono\">{expected}</dd></div>");
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
