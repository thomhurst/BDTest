using BDTest.Contracts;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Rendering;
using BDTest.Razor.Reports.Serialization;

namespace BDTest.Tests.Fixtures;

public class SharedPageRendererTests
{
    [Test]
    public async Task Every_historical_scenario_uses_the_same_complete_composition()
    {
        var report = Required(HostedReportJson.Deserialize(HistoricalReportFixture.ReadJson()));
        var standalone = ReportHtmlRenderer.GenerateHtmlReport(report);
        foreach (var scenario in report.Scenarios)
        {
            var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", scenario,
                configureViewData: data => data[nameof(ReportLayoutViewModel)] = ReportLayoutViewModel.Create(report));
            await Assert.That(standalone).Contains(ReportHtmlRenderer.RenderScenario(scenario));
            await Assert.That(hosted).Contains(ReportHtmlRenderer.RenderScenario(scenario, new ReportRenderOptions { Hosted = true }));
        }
    }

    [Test]
    public async Task Not_run_sections_use_the_same_compact_rows()
    {
        var report = new BDTestReport(1, "report", "tests", notRun:
            [new ScenarioDefinitionSummary("pending", "report", "Story <unsafe>", "Scenario <unsafe>",
                callerMember: "Method", callerFile: "C:/src/Example.cs")]);
        var pages = ReportPageModels.Create(report);
        var expected = ReportHtmlRenderer.RenderNotRun(report.NotRun);
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Warnings.cshtml", pages.Warnings,
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = pages.Warnings.Layout);
        await Assert.That(hosted).Contains(expected);
        await Assert.That(ReportHtmlRenderer.GenerateHtmlReport(report)).Contains(expected);
        await Assert.That(expected).Contains("&lt;unsafe&gt;");
        await Assert.That(expected).Contains("Example.cs");
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("https://untrusted.example/attachment")]
    [Arguments("//untrusted.example/attachment")]
    [Arguments("/\\untrusted.example/attachment")]
    [Arguments("/\nuntrusted.example/attachment")]
    public async Task Attachment_resolver_cannot_emit_an_active_or_remote_url(string url)
    {
        var html = ReportHtmlRenderer.RenderAttachments(
            [new AttachmentInfo("file", "capture.png", "image/png", 1, 1)],
            new ReportRenderOptions { AttachmentUrl = _ => url });
        await Assert.That(html).DoesNotContain("href=");
        await Assert.That(html).DoesNotContain("<img");
        await Assert.That(html).Contains("Content unavailable");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Scenario_pages_and_offline_reports_show_all_evidence(bool outline)
    {
        var attachment = new AttachmentInfo("112233445566778899aabbccddeeff00", "step-evidence.txt", "text/plain", 3, 1, uri: "javascript:untrusted");
        var failure = new ExceptionInfo("Failure", "failure-marker", innerExceptions: [new ExceptionInfo("Cause", "cause-marker")]);
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var step = new StepResult(StepType.Then, "Then evidence", Status.Failed, exception: failure,
            output: "step-output-marker", diagnostics: ["diagnostic-marker"], attachments: [attachment],
            startTime: start, endTime: start.AddSeconds(1), timeTaken: TimeSpan.FromSeconds(1));
        var row = new ScenarioResult("row", "report", "story", "scenario", Status.Failed, [step],
            attempts: [new ScenarioAttemptResult(1, Status.Failed, failure), new ScenarioAttemptResult(2, Status.Failed, failure)],
            testStartupInformation: "setup-marker", tearDownOutput: "teardown-marker", output: "output-marker",
            customTestInformation: ["information-marker"], retryCount: 1,
            customHtmlOutputForReport: "<p>custom-marker</p>");
        var scenario = outline ? new ScenarioResult("outline", "report", "story", "outline", Status.Failed, examples: [row], isOutline: true) : row;
        var report = new BDTestReport(1, "report", "tests", [scenario]);
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", scenario,
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = ReportLayoutViewModel.Create(report));
        var standalone = ReportHtmlRenderer.GenerateHtmlReport(report);
        await Assert.That(hosted).Contains(ReportHtmlRenderer.RenderScenario(scenario, new ReportRenderOptions { Hosted = true }));
        await Assert.That(standalone).Contains(ReportHtmlRenderer.RenderScenario(scenario));
        foreach (var html in new[] { standalone, hosted })
        {
            foreach (var marker in new[] { "step-output-marker", "diagnostic-marker", "step-evidence.txt", "failure-marker", "cause-marker", "setup-marker", "teardown-marker", "output-marker", "information-marker", "custom-marker", "Attempt 1", "Attempt 2" })
                await Assert.That(html).Contains(marker);
            await Assert.That(html).DoesNotContain("javascript:untrusted");
            await Assert.That(html).Contains("Time in scenario: 12:00:00.000 → 12:00:01.000 UTC");
        }
    }
}
