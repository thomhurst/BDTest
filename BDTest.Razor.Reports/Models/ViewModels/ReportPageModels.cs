using BDTest.Contracts;
using BDTest.Razor.Reports.Helpers;

namespace BDTest.Razor.Reports.Models.ViewModels;

/// <summary>A display row without steps, examples, attachments, output or execution state.</summary>
public sealed record ReportScenarioRow(string? Guid, string Name, string Story, Status Status, TimeSpan TimeTaken,
    int RetryCount, string? FailingStepText, int FailingStepNumber, string Explanation, string TimingName);

public sealed record ReportFailureGroup(string TypeName, string Title, string? Location, string? Trace,
    bool SameStep, bool Retried, int ScenarioCount, IReadOnlyList<ReportScenarioRow> Scenarios);

public sealed record ReportWarningRow(string Scenario, string Story, string? CallerFile, string? CallerMember);

public sealed record SummaryViewModel(ReportLayoutViewModel Layout, IReadOnlyList<ReportFailureGroup> Failures,
    int OtherCount, string OtherStatuses, IReadOnlyList<ReportScenarioRow> Others, IReadOnlyList<ReportScenarioRow> Slowest);

public sealed record TimingsViewModel(ReportLayoutViewModel Layout, int TimedScenarioCount, TimeSpan Median,
    TimeSpan Percentile95, double SlowestFiveShare, IReadOnlyList<ReportScenarioRow> Slowest,
    IReadOnlyList<ReportScenarioRow> Fastest);

public sealed record TopDefectsViewModel(ReportLayoutViewModel Layout,
    IReadOnlyDictionary<string, IReadOnlyList<ReportFailureGroup>> Groups)
{
    public IReadOnlyList<ReportFailureGroup> ForGrouping(string? groupBy) => Groups[FailureGroup.Normalize(groupBy)];
}

public sealed record WarningsViewModel(ReportLayoutViewModel Layout, IReadOnlyList<ReportWarningRow> NotRun);

/// <summary>Report-wide projections built together once per retained cache entry. No source graph is retained.</summary>
public sealed record ReportPageModels(SummaryViewModel Summary, TimingsViewModel Timings,
    TopDefectsViewModel TopDefects, WarningsViewModel Warnings)
{
    public static ReportPageModels Create(BDTestReport report)
    {
        var scenarios = (report.Scenarios ?? []).Where(scenario => scenario != null).ToList();
        // Share display rows across the three failure groupings and summary lists.
        var rows = new Dictionary<ScenarioResult, ReportScenarioRow>();
        ReportScenarioRow Row(ScenarioResult scenario)
        {
            if (rows.TryGetValue(scenario, out var row)) return row;
            var step = FailureGroup.FailingStep(scenario);
            var name = scenario.ScenarioText;
            var detail = scenario.Exception?.Message?.Trim().Split('\n')[0].Trim();
            var otherStep = scenario.Status == Status.Skipped ? null : scenario.Steps?.FirstOrDefault(s => s.Status == scenario.Status);
            var explanation = !string.IsNullOrEmpty(detail) ? scenario.Status.Label() + " · " + detail
                : otherStep == null ? scenario.Status.Label() : $"{scenario.Status.Label()} · step “{otherStep.StepText}”";
            row = new(scenario.Guid, name, scenario.StoryText, scenario.Status, scenario.TimeTaken,
                scenario.RetryCount, step?.StepText, FailureGroup.FailingStepNumber(scenario), explanation,
                scenario.ExampleValues?.Count > 0 ? name + " (" + string.Join(", ", scenario.ExampleValues.Values) + ")" : name);
            rows.Add(scenario, row);
            return row;
        }
        var groups = new Dictionary<string, IReadOnlyList<ReportFailureGroup>>();
        foreach (var grouping in new[] { FailureGroup.ByExceptionAndLocation, FailureGroup.ByExceptionType, FailureGroup.ByFailingStep })
        {
            groups.Add(grouping, Array.AsReadOnly(FailureGroup.Build(scenarios, grouping).Select(group =>
            {
                var members = group.Scenarios.Select(Row).ToArray();
                return new ReportFailureGroup(group.TypeName, group.Title, group.Location,
                    group.Exception?.AsString ?? group.Exception?.StackTrace,
                    members.Length > 1 && members.All(row => row.FailingStepText == members[0].FailingStepText),
                    members.Any(row => row.RetryCount > 0), members.Length, Array.AsReadOnly(members));
            }).ToArray()));
        }
        var layout = ReportLayoutViewModel.Create(report, scenarios, groups[FailureGroup.ByExceptionAndLocation].Count);
        var ordered = scenarios.Where(s => s.Status is not (Status.Skipped or Status.NotRun)).OrderBy(s => s.TimeTaken).ToList();
        TimeSpan Percentile(double fraction) => ordered.Count == 0 ? TimeSpan.Zero
            : ordered[Math.Clamp((int)Math.Ceiling(fraction * ordered.Count) - 1, 0, ordered.Count - 1)].TimeTaken;
        var slowest = ordered.AsEnumerable().Reverse().Take(10).ToArray();
        var totalTicks = ordered.Sum(s => s.TimeTaken.Ticks);
        var others = scenarios.Where(s => s.Status is not (Status.Passed or Status.Failed))
            .OrderBy(s => s.Status.GetOrder()).ThenBy(s => s.ScenarioText).ToList();
        return new(
            new(layout, Array.AsReadOnly(groups[FailureGroup.ByExceptionAndLocation].Take(3)
                    .Select(group => group with { Trace = null, Scenarios = Array.AsReadOnly(group.Scenarios.Take(5).ToArray()) }).ToArray()),
                others.Count, string.Join(",", others.Select(s => s.Status).Distinct()),
                Array.AsReadOnly(others.Take(10).Select(Row).ToArray()),
                Array.AsReadOnly(scenarios.OrderByDescending(s => s.TimeTaken).Take(5).Select(Row).ToArray())),
            new(layout, ordered.Count, Percentile(0.5), Percentile(0.95),
                totalTicks == 0 ? 0 : (double)slowest.Take(5).Sum(s => s.TimeTaken.Ticks) / totalTicks * 100,
                Array.AsReadOnly(slowest.Select(Row).ToArray()), Array.AsReadOnly(ordered.Take(10).Select(Row).ToArray())),
            new(layout, new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<ReportFailureGroup>>(groups)),
            new(layout, Array.AsReadOnly((report.NotRun ?? []).Where(test => test != null)
                .OrderBy(test => test.StoryText).ThenBy(test => test.ScenarioText)
                .Select(test => new ReportWarningRow(test.ScenarioText, test.StoryText, test.CallerFile, test.CallerMember)).ToArray())));
    }
}
