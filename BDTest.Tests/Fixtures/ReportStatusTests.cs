using BDTest.Razor.Reports.Extensions;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportStatusTests
{
    [TestCase(Status.NotRun, new Status[0])]
    [TestCase(Status.Skipped, new[] { Status.Skipped })]
    [TestCase(Status.Skipped, new[] { Status.Skipped, Status.Skipped })]
    [TestCase(Status.Passed, new[] { Status.Skipped, Status.Passed })]
    [TestCase(Status.Failed, new[] { Status.Skipped, Status.Failed })]
    [TestCase(Status.NotImplemented, new[] { Status.Skipped, Status.NotImplemented })]
    [TestCase(Status.Inconclusive, new[] { Status.Skipped, Status.Inconclusive })]
    [TestCase(Status.Failed, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Failed, Status.Skipped })]
    [TestCase(Status.NotImplemented, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Skipped })]
    [TestCase(Status.Inconclusive, new[] { Status.Passed, Status.Inconclusive, Status.Skipped })]
    [TestCase(Status.NotRun, new[] { Status.NotRun, Status.Passed, Status.Skipped })]
    [TestCase(Status.Pending, new[] { Status.Pending, Status.Passed, Status.Skipped })]
    [TestCase(Status.NotRun, new[] { Status.Pending, Status.NotRun, Status.Passed })]
    [TestCase(Status.Inconclusive, new[] { Status.NotRun, Status.Pending, Status.Inconclusive })]
    public void TotalStatusReflectsScenarioOutcomes(Status expected, Status[] statuses)
    {
        var scenarios = statuses.Select(status =>
            ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new { Status = status })));

        Assert.That(scenarios.GetTotalStatus(), Is.EqualTo(expected));
    }

    [TestCase(0, Status.Passed)]
    [TestCase(1, Status.Failed)]
    [TestCase(2, Status.Inconclusive)]
    [TestCase(3, Status.NotImplemented)]
    [TestCase(4, Status.Skipped)]
    public void LegacyNumericSummariesKeepTheirMeaningAndAreWrittenAsNames(int value, Status expected)
    {
        var summary = ReportJson.DeserializeObject<BDTest.Razor.Reports.Models.TestRunSummary>($"{{\"Status\":{value}}}");
        Assert.That(summary.Status, Is.EqualTo(expected));
        var json = ReportJson.SerializeObject(summary);
        Assert.That(json, Does.Contain($"\"Status\":\"{expected}\""));
        Assert.That(ReportJson.DeserializeObject<BDTest.Razor.Reports.Models.TestRunSummary>(json).Status, Is.EqualTo(expected));
    }
}
