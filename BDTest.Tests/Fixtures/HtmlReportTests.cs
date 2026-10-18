using System.Net;
using System.Text.RegularExpressions;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class HtmlReportTests : BDTestBase
{
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

    [TestCase(false)]
    [TestCase(true)]
    public void MissingDescriptorValuesUseConfiguredDefaults(bool nullDescriptor)
    {
#pragma warning disable CS0618 // Verify compatibility with the JSON report's legacy defaults.
        var environment = BDTestSettings.Environment;
        var tag = BDTestSettings.Tag;
        var branch = BDTestSettings.BranchName;
        try
        {
            BDTestSettings.Environment = "Configured environment";
            BDTestSettings.Tag = "Configured tag";
            BDTestSettings.BranchName = "Configured branch";
            var html = nullDescriptor
                ? BDTestHtmlHelper.GetTestHtmlReport(null)
                : BDTestHtmlHelper.GetTestHtmlReport();

            Assert.That(html, Does.Contain("<dt>Environment</dt><dd>Configured environment</dd>"));
            Assert.That(html, Does.Contain("<dt>Tag</dt><dd>Configured tag</dd>"));
            Assert.That(html, Does.Contain("<dt>Branch</dt><dd class=\"mono\">Configured branch</dd>"));
        }
        finally
        {
            BDTestSettings.Environment = environment;
            BDTestSettings.Tag = tag;
            BDTestSettings.BranchName = branch;
        }
#pragma warning restore CS0618
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
    public void CustomHtmlIsIsolatedInAScriptlessFrame()
    {
        const string payload = "<script>parent.document.body.innerHTML = 'compromised';</script>";
        When(() => WriteCustomHtml(payload)).Then(() => DoNothing()).BDTest();

        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var frame = Regex.Match(html, "<iframe class=\"custom-body\"[^>]*>");

        Assert.That(frame.Success, Is.True);
        Assert.That(frame.Value, Does.Contain("sandbox=\"\""));
        Assert.That(frame.Value, Does.Contain("Content-Security-Policy"));
        Assert.That(html, Does.Not.Contain(payload));
    }

    [Test]
    public void FailuresWithTheSameErrorAreGroupedIntoOneCause()
    {
        Assert.Catch(() => When(() => Fail()).Then(() => DoNothing()).WithScenarioText("First").BDTest());
        Assert.Catch(() => When(() => Fail()).Then(() => DoNothing()).WithScenarioText("Second").BDTest());

        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.Contain("Needs attention"));
        Assert.That(Regex.Matches(html, "<article class=\"cause\">").Count, Is.EqualTo(1));
        Assert.That(html, Does.Contain("<span class=\"cause-n\">2 scenarios</span>"));
        Assert.That(html, Does.Contain("<span class=\"v-word\">2 failed</span>"));
    }

    [Test]
    [BDTestTag("api\tintegration\nline\rreturn\0null\bback\fform\\slash\"quote<>&'")]
    public void TagMetadataRoundTripsControlCharactersAndHtml()
    {
        var scenario = When(() => DoNothing()).Then(() => DoNothing()).BDTest();
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var attribute = Regex.Match(html, "data-tags=\"([^\"]*)\"").Groups[1].Value;

        Assert.That(attribute, Is.Not.Empty);
        var json = WebUtility.HtmlDecode(attribute);
        Assert.That(json.Any(char.IsControl), Is.False, "JSON must escape literal control characters.");
        Assert.That(JsonConvert.DeserializeObject<string[]>(json), Is.EqualTo(scenario.Tags));
    }

    [Test]
    public void PassingRunHasNoAttentionSection()
    {
        When(() => DoNothing()).Then(() => DoNothing()).BDTest();

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
    private static void DoNothing() { }
    private static void NotImplemented() => throw new NotImplementedException("Not implemented yet");
    private static void Fail() => throw new InvalidOperationException("<failure>");
}
