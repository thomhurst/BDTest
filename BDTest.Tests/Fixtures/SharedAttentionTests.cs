using BDTest.Contracts;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Rendering;

namespace BDTest.Tests.Fixtures;

public class SharedAttentionTests
{
    [Test]
    public async Task Standalone_attention_uses_shared_cards_and_offline_navigation()
    {
        var exception = new ExceptionInfo("Failure <unsafe>", "Problem <unsafe>");
        var scenario = new ScenarioResult("one", "run", "Story", "Scenario <unsafe>", Status.Failed, exception: exception);
        var groups = new[] { new ReportFailureOverview(exception.TypeName, exception.Message, null, 1,
            [new ReportFailureLink(scenario.Guid, scenario.ScenarioText)]) };
        var fragment = ReportHtmlRenderer.RenderAttention(groups, 1);
        var standalone = ReportHtmlRenderer.GenerateHtmlReport(new BDTestReport(1, "run", "tests", [scenario]));
        await Assert.That(standalone).Contains(fragment);
        await Assert.That(fragment).Contains("data-go=\"one\"");
        await Assert.That(fragment).Contains("Scenario &lt;unsafe&gt;");
        var hosted = ReportHtmlRenderer.RenderAttention(groups, 1, scenarioUrl: id => "/reports/scenario/" + id);
        await Assert.That(hosted).Contains("href=\"/reports/scenario/one\"");
        await Assert.That(hosted).DoesNotContain("data-go=");
        foreach (var html in new[] { hosted, standalone })
        {
            await Assert.That(html).Contains("Problem &lt;unsafe&gt;");
            await Assert.That(html).Contains("<span class=\"cause-n\">1 scenario</span>");
            await Assert.That(html).DoesNotContain("<unsafe>");
        }
    }

    [Test]
    public async Task Hosted_attention_keeps_compact_limits_and_more_links_around_shared_cards()
    {
        var report = new BDTestReport(1, "run", "tests", Enumerable.Range(0, 4).SelectMany(group =>
            Enumerable.Range(0, 7).Select(row => new ScenarioResult($"scenario-{group}-{row}", "run", "Story",
                $"Scenario {group}-{row}", Status.Failed, exception: new ExceptionInfo("Failure", $"Cause {group}"),
                timeTaken: TimeSpan.FromSeconds(1)))).ToArray());
        var pages = ReportPageModels.Create(report);
        var groups = pages.Summary.Failures.Take(3).Select(group => new ReportFailureOverview(group.TypeName,
            group.Title, group.Location, group.ScenarioCount, group.Scenarios.Take(5).Select(scenario =>
                new ReportFailureLink(scenario.Guid!, scenario.Name, "Story", scenario.TimeTaken)).ToArray())).ToArray();
        var expected = ReportHtmlRenderer.RenderAttention(groups, 28, 4,
            id => "/reports/bdtest/report/run/scenario/" + id, "/reports/bdtest/report/run/failures");
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Summary.cshtml", pages.Summary,
            pathBase: "/reports", configureViewData: data => data[nameof(ReportLayoutViewModel)] = pages.Summary.Layout);
        await Assert.That(hosted).Contains(expected);
        await Assert.That(expected).Contains("2 more</a>");
        await Assert.That(expected).Contains("1 more cause</a>");
        await Assert.That(expected).DoesNotContain("Cause 3");
        await Assert.That(expected).DoesNotContain("scenario-0-5");
        await Assert.That(pages.Summary.Failures.All(group => group.Scenarios.Count <= 5)).IsTrue();
    }
}
