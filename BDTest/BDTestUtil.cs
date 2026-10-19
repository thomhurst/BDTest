using BDTest.Helpers;
using BDTest.Contracts;
using ScenarioDefinitionSummary = BDTest.Contracts.ScenarioDefinitionSummary;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;

namespace BDTest;

public static class BDTestUtil
{
    /// <summary>Gets a snapshot of the run's top-level scenarios.</summary>
    public static IReadOnlyList<ScenarioResult> GetScenarios(BDTestRun run) =>
        (run ?? throw new ArgumentNullException(nameof(run))).Store.ScenariosByInternalId.Values
        .Where(scenario => scenario.OutlineId == null).Select(ResultSnapshot.Create).ToList().AsReadOnly();

    /// <summary>Gets a snapshot of the run's unfinished scenario definitions.</summary>
    public static IReadOnlyList<ScenarioDefinitionSummary> GetNotRunScenarios(BDTestRun run) =>
        (run ?? throw new ArgumentNullException(nameof(run))).Store.NotRun.Values.Select(ResultSnapshot.Create).ToList().AsReadOnly();

    /// <summary>Clears this run's scenarios, pending definitions, listeners, output, and temporary attachment files.</summary>
    /// <remarks>Call between executions. Does not cancel tests or change the report identifier.</remarks>
    public static void ClearScenarios(BDTestRun run) => (run ?? throw new ArgumentNullException(nameof(run))).Clear();

    /// <summary>Gets the calendar span of immutable scenario snapshots.</summary>
    public static ReportTimer GetTestTimer(IReadOnlyCollection<ScenarioResult> scenarios) => new(
        scenarios.Select(scenario => scenario.StartTime).Where(time => time != default).DefaultIfEmpty().Min(),
        scenarios.Select(scenario => scenario.EndTime).Where(time => time != default).DefaultIfEmpty().Max());

    public static TestTimer GetTestTimer(IReadOnlyCollection<Scenario> scenarios)
    {
        return GetTestTimer(scenarios, null);
    }

    public static TestTimer GetTestTimer(IReadOnlyCollection<Scenario> scenarios,
        BDTestOutputModel? totalReportData)
    {
        return scenarios.Count switch
        {
            0 when totalReportData?.TestTimer != null => new TestTimer
            {
                TestsStartedAt = totalReportData.TestTimer.TestsStartedAt,
                TestsFinishedAt = totalReportData.TestTimer.TestsFinishedAt
            },
            0 => new TestTimer(),
            _ => new TestTimer
            {
                TestsStartedAt = scenarios.GetStartDateTime(), TestsFinishedAt = scenarios.GetEndDateTime()
            }
        };
    }

    internal static DateTime GetStartDateTime(this IEnumerable<Scenario> scenarios)
    {
        return scenarios.Select(scenario => scenario.StartTime).Where(time => time != default).DefaultIfEmpty().Min();
    }

    internal static DateTime GetEndDateTime(this IEnumerable<Scenario> scenarios)
    {
        return scenarios.Select(scenario => scenario.EndTime).Where(time => time != default).DefaultIfEmpty().Max();
    }
}
