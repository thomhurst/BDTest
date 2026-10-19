using System.Text.RegularExpressions;
using BDTest.Contracts;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Serialization;

namespace BDTest.Tests.Fixtures;

public class HostedReportPresentationTests
{
    [Test]
    public async Task Summary_timestamps_preserve_instants_across_different_offsets()
    {
        var start = new DateTimeOffset(2026, 10, 19, 14, 0, 0, TimeSpan.FromHours(2));
        var finish = new DateTimeOffset(2026, 10, 19, 8, 1, 0, TimeSpan.FromHours(-4));
        var report = new BDTestReport(1, "offsets", "tests", testTimer: new ReportTimer(start, finish));
        var restored = Required(HostedReportJson.Deserialize(BDTestReportJson.Serialize(report)));
        var summary = new TestRunSummary(restored);

        await Assert.That(summary.StartedAtDateTime).IsEqualTo(start.UtcDateTime);
        await Assert.That(summary.FinishedAtDateTime).IsEqualTo(finish.UtcDateTime);
        await Assert.That(summary.StartedAtDateTime.Kind).IsEqualTo(DateTimeKind.Utc);
        await Assert.That(summary.FinishedAtDateTime - summary.StartedAtDateTime).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    [Arguments(false, "same")]
    [Arguments(true, "same")]
    [Arguments(false, "data")]
    [Arguments(true, "data")]
    [Arguments(false, "inner")]
    [Arguments(true, "inner")]
    public async Task Deserialized_failures_render_once_only_when_all_details_match(bool outline, string difference)
    {
        var stepException = new ExceptionInfo("Failure", "failure", "unique-stack-marker",
            data: new Dictionary<string, string> { ["first"] = "one", ["second"] = "two" },
            innerExceptions: [new ExceptionInfo("Cause", "cause")]);
        var scenarioException = new ExceptionInfo("Failure", "failure", "unique-stack-marker",
            data: new Dictionary<string, string> { ["second"] = "two", ["first"] = difference == "data" ? "changed" : "one" },
            innerExceptions: [new ExceptionInfo("Cause", difference == "inner" ? "different cause" : "cause")]);
        var row = new ScenarioResult("row", "report", "story", "scenario", Status.Failed,
            [new StepResult(StepType.Then, "Then fail", Status.Failed, exception: stepException)], exception: scenarioException);
        var scenario = outline
            ? new ScenarioResult("outline", "report", "story", "outline", Status.Failed, examples: [row], isOutline: true)
            : row;
        var report = new BDTestReport(1, "report", "tests", [scenario]);
        var restored = Required(HostedReportJson.Deserialize(BDTestReportJson.Serialize(report)));
        var restoredRow = outline ? restored.Scenarios[0].Examples[0] : restored.Scenarios[0];
        await Assert.That(ReferenceEquals(restoredRow.Exception, restoredRow.Steps[0].Exception)).IsFalse();

        var html = await ReportHandlerEncodingTests.RenderAsync(
            outline ? "/Views/Shared/_ScenarioExamples.cshtml" : "/Views/BDTest/Scenario.cshtml",
            restored.Scenarios[0]);

        await Assert.That(Regex.Matches(html, "unique-stack-marker").Count).IsEqualTo(difference == "same" ? 1 : 2);
    }
}
