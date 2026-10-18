using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class RunFilterTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static TestRunSummary Run(string id, Status status, string tag = "nightly", int daysAgo = 1)
    {
        var run = ReportJson.DeserializeObject<TestRunSummary>("{}");
        run.RecordId = id;
        run.Status = status;
        run.Tag = tag;
        run.StartedAtDateTime = Now.AddDays(-daysAgo);
        return run;
    }

    private static RunFilter Parse(string query) =>
        RunFilter.FromQuery(new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)));

    [TestCase("Passed", new[] { "passed", "not-implemented", "skipped" })]
    [TestCase("Failed", new[] { "failed" })]
    public void Status_filter_matches_the_result_shown_for_each_run(string status, string[] expected)
    {
        var runs = new[]
        {
            Run("passed", Status.Passed), Run("failed", Status.Failed), Run("inconclusive", Status.Inconclusive),
            Run("pending", Status.Pending), Run("not-run", Status.NotRun), Run("not-implemented", Status.NotImplemented),
            Run("skipped", Status.Skipped)
        };

        var matched = Parse("?status=" + status).Apply(runs, Now).Select(run => run.RecordId);

        Assert.That(matched, Is.EquivalentTo(expected));
    }

    [Test]
    public void Mixed_incomplete_runs_never_match_Passed_even_when_the_aggregate_is_NotImplemented()
    {
        var runs = new[]
        {
            Run("inconclusive", Status.NotImplemented), Run("pending", Status.NotImplemented),
            Run("not-run", Status.NotImplemented), Run("failed", Status.NotImplemented),
            Run("complete", Status.NotImplemented)
        };
        foreach (var run in runs)
            run.Counts.NotImplemented = 1;
        runs[0].Counts.Inconclusive = 1;
        runs[1].Counts.Pending = 1;
        runs[2].Counts.NotRun = 1;
        runs[3].Counts.Failed = 1;

        Assert.That(Parse("?status=Passed").Apply(runs, Now).Select(run => run.RecordId),
            Is.EqualTo(new[] { "complete" }));
        Assert.That(Parse("?status=Failed").Apply(runs, Now).Select(run => run.RecordId),
            Is.EqualTo(new[] { "failed" }));
    }

    [Test]
    public void Values_that_start_with_All_are_real_filters_but_old_placeholders_are_ignored()
    {
        var runs = new[] { Run("browsers", Status.Passed, "All browsers"), Run("other", Status.Passed, "other") };

        Assert.That(Parse("?tag=All%20browsers&q=All%20b").Apply(runs, Now).Select(run => run.RecordId), Is.EqualTo(new[] { "browsers" }));
        Assert.That(Parse("?tag=All%20Tags").Apply(runs, Now), Has.Exactly(2).Items);
    }

    [Test]
    public void Custom_dates_are_kept_until_another_range_is_chosen()
    {
        const string dates = "2026-10-01T00:00:00.0000000Z..2026-10-10T00:00:00.0000000Z";

        var legacy = Parse("?datetimerange=" + dates);
        Assert.That(legacy.CustomRange, Is.Not.Null);
        Assert.That(legacy.ToQuery().ToDictionary(pair => pair.Key, pair => pair.Value), Does.ContainKey("datetimerange").And.ContainValue(RunFilter.Custom));

        Assert.That(Parse("?range=custom&tag=nightly&datetimerange=" + dates).CustomRange, Is.Not.Null);

        var replaced = Parse("?range=7d&datetimerange=" + dates);
        Assert.That(replaced.CustomRange, Is.Null);
        Assert.That(replaced.Range, Is.EqualTo(RunFilter.Last7Days));
    }
}
