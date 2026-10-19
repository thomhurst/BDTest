using BDTest.Contracts;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Razor.Reports.Serialization;
using BDTest.Rendering;

namespace BDTest.Tests.Fixtures;

public class SharedRunOverviewTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Standalone_and_hosted_summary_render_the_same_compact_overview(bool historical)
    {
        var report = historical
            ? Required(HostedReportJson.Deserialize(HistoricalReportFixture.ReadJson()))
            : CurrentReport();
        var before = BDTestReportJson.Serialize(report);
        var pages = ReportPageModels.Create(report);
        var overview = ReportRunOverview.FromReport(report);
        var path = "/reports/bdtest/report/" + Uri.EscapeDataString(report.Id);
        string? StatusUrl(Status? status) => path + "/all-scenarios"
            + (status.HasValue ? "?filterByStatus=" + status.Value : string.Empty);
        var expected = ReportHtmlRenderer.RenderRunOverview(overview, StatusUrl, path + "/not-run");
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Summary.cshtml", pages.Summary,
            pathBase: "/reports", requestPath: path + "/summary",
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = pages.Summary.Layout);
        await Assert.That(hosted).Contains(expected);
        await Assert.That(ReportHtmlRenderer.GenerateHtmlReport(report)).Contains(ReportHtmlRenderer.RenderRunOverview(overview));
        await Assert.That(hosted).Contains("Slowest scenarios");
        await Assert.That(hosted).Contains("Run identity");
        await Assert.That(BDTestReportJson.Serialize(report)).IsEqualTo(before);
        if (!historical)
        {
            await Assert.That(hosted).Contains("1 failed");
            await Assert.That(hosted).Contains("of 8 scenarios");
            await Assert.That(hosted).Contains("built but never run");
            await Assert.That(hosted).Contains("2026-10-18 23:02:03");
            await Assert.That(hosted).Contains("&lt;unsafe&gt;");
            await Assert.That(hosted).DoesNotContain("<unsafe>");
            foreach (Status status in Enum.GetValues<Status>())
                await Assert.That(hosted).Contains("filterByStatus=" + status);
        }
    }

    [Test]
    public async Task Empty_overviews_show_missing_timing_and_unexecuted_definitions_consistently()
    {
        var report = new BDTestReport(1, "empty", "tests", notRun:
            [new ScenarioDefinitionSummary("pending", "empty", "story", "declared")]);
        var overview = ReportRunOverview.FromReport(report);
        var html = ReportHtmlRenderer.RenderRunOverview(overview);
        await Assert.That(html).Contains("No scenarios ran");
        await Assert.That(html).Contains("No timing recorded");
        await Assert.That(html).Contains("Built but never run");
        var pages = ReportPageModels.Create(report);
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Summary.cshtml", pages.Summary,
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = pages.Summary.Layout);
        await Assert.That(hosted).Contains("No scenarios ran");
        await Assert.That(hosted).Contains("No timing recorded");
    }

    [Test]
    public async Task Compact_layout_prefers_complete_report_timer()
    {
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var scenario = new ScenarioResult("one", "timer", "story", "scenario", Status.Passed,
            startTime: start.AddSeconds(10), endTime: start.AddSeconds(20));
        var timer = new ReportTimer(start, start.AddMinutes(1));
        var explicitLayout = ReportPageModels.Create(new BDTestReport(1, "timer", "tests", [scenario], testTimer: timer)).Summary.Layout;
        await Assert.That(explicitLayout.Timer.TestsStartedAt).IsEqualTo(start);
        await Assert.That(explicitLayout.Timer.TestsFinishedAt).IsEqualTo(start.AddMinutes(1));
        var fallback = ReportPageModels.Create(new BDTestReport(1, "timer", "tests", [scenario])).Summary.Layout;
        await Assert.That(fallback.Timer.TestsStartedAt).IsEqualTo(scenario.StartTime);
        await Assert.That(fallback.Timer.TestsFinishedAt).IsEqualTo(scenario.EndTime);
    }

    [Test]
    public async Task Hosted_summary_preserves_timing_inferred_from_scenarios()
    {
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 10, TimeSpan.Zero);
        var scenario = new ScenarioResult("one", "timer", "story", "scenario", Status.Passed,
            startTime: start, endTime: start.AddSeconds(10));
        var pages = ReportPageModels.Create(new BDTestReport(1, "timer", "tests", [scenario]));
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Summary.cshtml", pages.Summary,
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = pages.Summary.Layout);
        await Assert.That(hosted).DoesNotContain("No timing recorded");
        await Assert.That(hosted).Contains("2026-10-19 12:00:10 → 12:00:20 UTC");
    }

    [Test]
    [Arguments(0, true)]
    [Arguments(1, true)]
    [Arguments(2, true)]
    [Arguments(3, true)]
    [Arguments(4, true)]
    [Arguments(1, false)]
    [Arguments(2, false)]
    [Arguments(3, false)]
    [Arguments(4, false)]
    public async Task Overview_timer_fallback_matches_hosted_layout(int kind, bool hasScenarios)
    {
        var start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        ReportTimer? timer = kind switch
        {
            1 => new(default, default),
            2 => new(start.AddMinutes(-1), default),
            3 => new(default, start.AddMinutes(1)),
            4 => new(start.AddMinutes(-1), start.AddMinutes(1)),
            _ => null
        };
        var scenarios = hasScenarios ? new[] {
            new ScenarioResult("untimed", "timer", "story", "untimed", Status.Passed),
            new ScenarioResult("one", "timer", "story", "one", Status.Passed,
                startTime: start, endTime: start.AddSeconds(10)),
            new ScenarioResult("two", "timer", "story", "two", Status.Passed,
                startTime: start.AddSeconds(2), endTime: start.AddSeconds(20))
        } : Array.Empty<ScenarioResult>();
        var report = new BDTestReport(1, "timer", "tests", scenarios, testTimer: timer);
        var expected = hasScenarios && kind != 4 ? new ReportTimer(start, start.AddSeconds(20)) : timer!;
        var overview = ReportRunOverview.FromReport(report);
        var layout = ReportPageModels.Create(report).Summary.Layout;
        await Assert.That(overview.Timer!.TestsStartedAt).IsEqualTo(expected.TestsStartedAt);
        await Assert.That(overview.Timer.TestsFinishedAt).IsEqualTo(expected.TestsFinishedAt);
        await Assert.That(layout.Timer.TestsStartedAt).IsEqualTo(expected.TestsStartedAt);
        await Assert.That(layout.Timer.TestsFinishedAt).IsEqualTo(expected.TestsFinishedAt);
    }

    [Test]
    public async Task Projection_copies_mutable_inputs_and_contains_no_scenario_graph()
    {
        var counts = new Dictionary<Status, int> { [Status.Passed] = 1 };
        var properties = new Dictionary<string, string> { ["build"] = "first" };
        var overview = new ReportRunOverview(1, 0, counts, customProperties: properties);
        counts[Status.Passed] = 5;
        properties["build"] = "changed";
        await Assert.That(overview.Counts[Status.Passed]).IsEqualTo(1);
        await Assert.That(overview.CustomProperties["build"]).IsEqualTo("first");
        var graphTypes = new[] { typeof(BDTestReport), typeof(ScenarioResult), typeof(StepResult), typeof(ExceptionInfo) };
        foreach (var property in typeof(ReportRunOverview).GetProperties())
            await Assert.That(graphTypes.Contains(property.PropertyType)).IsFalse();
    }

    [Test]
    [Arguments("javascript:alert(1)")]
    [Arguments("data:text/html,test")]
    [Arguments("/safe\r\nunsafe")]
    public async Task Overview_navigation_rejects_executable_schemes_and_controls(string url)
    {
        var overview = ReportRunOverview.FromReport(CurrentReport());
        await Assert.That(() => ReportHtmlRenderer.RenderRunOverview(overview, _ => url)).Throws<ArgumentException>();
        await Assert.That(() => ReportHtmlRenderer.RenderRunOverview(overview, notRunUrl: url)).Throws<ArgumentException>();
    }

    private static BDTestReport CurrentReport()
    {
        var start = new DateTimeOffset(2026, 10, 19, 1, 2, 3, TimeSpan.FromHours(2));
        return new BDTestReport(1, "O'Brien", "version <unsafe>",
            Enum.GetValues<Status>().Select(status => new ScenarioResult(status.ToString(), "O'Brien", "story",
                "Scenario " + status, status)).ToArray(),
            [new ScenarioDefinitionSummary("pending", "O'Brien", "story", "declared")],
            environment: "CI <unsafe>", tag: "tag <unsafe>", branchName: "branch <unsafe>", machineName: "machine <unsafe>",
            testTimer: new ReportTimer(start, start.AddSeconds(30)),
            customProperties: new Dictionary<string, string> { ["property <unsafe>"] = "value <unsafe>" });
    }
}
