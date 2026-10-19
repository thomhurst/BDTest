using BDTest.Razor.Reports.Extensions;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportStatusTests
{
    [Test]
    [Arguments(Status.NotRun, new Status[0])]
    [Arguments(Status.Skipped, new[] { Status.Skipped })]
    [Arguments(Status.Skipped, new[] { Status.Skipped, Status.Skipped })]
    [Arguments(Status.Passed, new[] { Status.Skipped, Status.Passed })]
    [Arguments(Status.Failed, new[] { Status.Skipped, Status.Failed })]
    [Arguments(Status.NotImplemented, new[] { Status.Skipped, Status.NotImplemented })]
    [Arguments(Status.Inconclusive, new[] { Status.Skipped, Status.Inconclusive })]
    [Arguments(Status.Failed, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Failed, Status.Skipped })]
    [Arguments(Status.NotImplemented, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Skipped })]
    [Arguments(Status.Inconclusive, new[] { Status.Passed, Status.Inconclusive, Status.Skipped })]
    [Arguments(Status.NotRun, new[] { Status.NotRun, Status.Passed, Status.Skipped })]
    [Arguments(Status.Pending, new[] { Status.Pending, Status.Passed, Status.Skipped })]
    [Arguments(Status.NotRun, new[] { Status.Pending, Status.NotRun, Status.Passed })]
    [Arguments(Status.Inconclusive, new[] { Status.NotRun, Status.Pending, Status.Inconclusive })]
    public async Task TotalStatusReflectsScenarioOutcomes(Status expected, Status[] statuses)
    {
        var scenarios = statuses.Select(status =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new { Status = status })));

        await Assert.That(scenarios.GetTotalStatus()).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, Status.Passed)]
    [Arguments(1, Status.Failed)]
    [Arguments(2, Status.Inconclusive)]
    [Arguments(3, Status.NotImplemented)]
    [Arguments(4, Status.Skipped)]
    public async Task LegacyNumericSummariesKeepTheirMeaningAndAreWrittenAsNames(int value, Status expected)
    {
        var summary = ReportJson.DeserializeObject<BDTest.Razor.Reports.Models.TestRunSummary>($"{{\"Status\":{value}}}");
        await Assert.That(summary.Status).IsEqualTo(expected);
        var json = ReportJson.SerializeObject(summary);
        await Assert.That(json).Contains($"\"Status\":\"{expected}\"");
        await Assert.That(ReportJson.DeserializeObject<BDTest.Razor.Reports.Models.TestRunSummary>(json).Status).IsEqualTo(expected);
    }
}
