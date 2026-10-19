using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;

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

    [Test]
    [Arguments("Passed", new[] { "passed", "not-implemented", "skipped" })]
    [Arguments("Failed", new[] { "failed" })]
    public async Task Status_filter_matches_the_result_shown_for_each_run(string status, string[] expected)
    {
        var runs = new[]
        {
            Run("passed", Status.Passed), Run("failed", Status.Failed), Run("inconclusive", Status.Inconclusive),
            Run("pending", Status.Pending), Run("not-run", Status.NotRun), Run("not-implemented", Status.NotImplemented),
            Run("skipped", Status.Skipped)
        };

        var matched = Parse("?status=" + status).Apply(runs, Now).Select(run => run.RecordId);


        await Assert.That(matched).IsEquivalentTo((expected).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task Mixed_incomplete_runs_never_match_Passed_even_when_the_aggregate_is_NotImplemented()
    {
        var runs = new[]
        {
            Run("inconclusive", Status.NotImplemented), Run("pending", Status.NotImplemented),
            Run("not-run", Status.NotImplemented), Run("failed", Status.NotImplemented),
            Run("complete", Status.NotImplemented)
        };
        foreach (var run in runs)
            Required(run.Counts).NotImplemented = 1;
        Required(runs[0].Counts).Inconclusive = 1;
        Required(runs[1].Counts).Pending = 1;
        Required(runs[2].Counts).NotRun = 1;
        Required(runs[3].Counts).Failed = 1;

        await Assert.That(Parse("?status=Passed").Apply(runs, Now).Select(run => run.RecordId)).IsEquivalentTo((new[] { "complete" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Parse("?status=Failed").Apply(runs, Now).Select(run => run.RecordId)).IsEquivalentTo((new[] { "failed" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Values_that_start_with_All_are_real_filters_but_old_placeholders_are_ignored()
    {
        var runs = new[] { Run("browsers", Status.Passed, "All browsers"), Run("other", Status.Passed, "other") };

        await Assert.That(Parse("?tag=All%20browsers&q=All%20b").Apply(runs, Now).Select(run => run.RecordId)).IsEquivalentTo((new[] { "browsers" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Parse("?tag=All%20Tags").Apply(runs, Now)).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Custom_dates_are_kept_until_another_range_is_chosen()
    {
        const string dates = "2026-10-01T00:00:00.0000000Z..2026-10-10T00:00:00.0000000Z";

        var legacy = Parse("?datetimerange=" + dates);
        await Assert.That(legacy.CustomRange).IsNotNull();

        await Assert.That(legacy.ToQuery().ToDictionary(pair => pair.Key, pair => pair.Value)).ContainsKey("datetimerange").And.ContainsValue(RunFilter.Custom);

        await Assert.That(Parse("?range=custom&tag=nightly&datetimerange=" + dates).CustomRange).IsNotNull();

        var replaced = Parse("?range=7d&datetimerange=" + dates);
        await Assert.That(replaced.CustomRange).IsNull();
        await Assert.That(replaced.Range).IsEqualTo(RunFilter.Last7Days);
    }
}
