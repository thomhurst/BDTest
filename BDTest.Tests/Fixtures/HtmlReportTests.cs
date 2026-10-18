using System.Text.RegularExpressions;
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
    public void SetUp() => TestResetHelper.ResetData();

    [Test]
    public void EmptyReportIsSelfContained()
    {
        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
        Assert.That(html, Does.Contain("<span class=\"count\">0</span><span class=\"label\">Total</span>"));
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

            Assert.That(html, Does.Contain("<strong>Environment:</strong> Configured environment"));
            Assert.That(html, Does.Contain("<strong>Tag:</strong> Configured tag"));
            Assert.That(html, Does.Contain("<strong>Branch:</strong> Configured branch"));
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
        Assert.That(html, Does.Contain($"<strong>Environment:</strong> {encodedText}"));
        Assert.That(html, Does.Contain($"class=\"scenario-title\">{encodedText}"));
        Assert.That(html, Does.Contain("<strong>Tag:</strong> Explicit tag"));
        Assert.That(html, Does.Contain("<strong>Branch:</strong> Explicit branch"));
        Assert.That(html, Does.Contain($"id=\"details-{scenario.Guid}\""));
    }

    [Test]
    public async Task NotImplementedScenarioMatchesItsFilter()
    {
        await Assert.CatchAsync(async () => await When(() => NotImplemented()).Then(() => DoNothing()).BDTestAsync());
        Assert.That(BDTestUtil.GetScenarios().Single().Status, Is.EqualTo(Status.NotImplemented));
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var scenarioStatus = Regex.Match(html, "class=\"scenario-item [^\"]+\" data-status=\"([^\"]+)\"").Groups[1].Value;
        var filterStatus = Regex.Match(html, "data-status=\"([^\"]+)\">Not Implemented</button>").Groups[1].Value;

        Assert.That(filterStatus, Is.Not.Empty);
        Assert.That(filterStatus, Is.EqualTo(scenarioStatus));
        Assert.That(html, Does.Contain("<span class=\"count\">1</span><span class=\"label\">Not Implemented</span>"));
    }

    [Test]
    public async Task StepsAfterFailureAreNotReportedAsPassed()
    {
        await Assert.CatchAsync(async () => await When(() => Fail()).Then(() => DoNothing()).BDTestAsync());
        var scenario = BDTestUtil.GetScenarios().Single();
        Assert.That(scenario.Steps.Select(step => step.Status), Is.EqualTo(new[] { Status.Failed, Status.NotRun }));
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var stepStatuses = Regex.Matches(html, "class=\"step ([^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToArray();

        Assert.That(stepStatuses, Is.EqualTo(new[] { "failed", "notrun" }));
        Assert.That(html, Does.Contain("&lt;failure&gt;"));
        Assert.That(html, Does.Not.Contain("<failure>"));
    }

    private static void DoNothing() { }
    private static void NotImplemented() => throw new NotImplementedException("Not implemented yet");
    private static void Fail() => throw new InvalidOperationException("<failure>");
}
