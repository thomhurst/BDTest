using BDTest.Razor.Reports.Extensions;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportStatusTests
{
    [TestCase(Status.Inconclusive, new Status[0])]
    [TestCase(Status.Skipped, new[] { Status.Skipped })]
    [TestCase(Status.Skipped, new[] { Status.Skipped, Status.Skipped })]
    [TestCase(Status.Passed, new[] { Status.Skipped, Status.Passed })]
    [TestCase(Status.Failed, new[] { Status.Skipped, Status.Failed })]
    [TestCase(Status.NotImplemented, new[] { Status.Skipped, Status.NotImplemented })]
    [TestCase(Status.Inconclusive, new[] { Status.Skipped, Status.Inconclusive })]
    [TestCase(Status.Failed, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Failed, Status.Skipped })]
    [TestCase(Status.NotImplemented, new[] { Status.Passed, Status.Inconclusive, Status.NotImplemented, Status.Skipped })]
    [TestCase(Status.Inconclusive, new[] { Status.Passed, Status.Inconclusive, Status.Skipped })]
    public void TotalStatusReflectsScenarioOutcomes(Status expected, Status[] statuses)
    {
        var scenarios = statuses.Select(status =>
            JsonConvert.DeserializeObject<Scenario>(JsonConvert.SerializeObject(new { Status = status })));

        Assert.That(scenarios.GetTotalStatus(), Is.EqualTo(expected));
    }
}
