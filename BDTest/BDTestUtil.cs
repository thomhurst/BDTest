using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;

namespace BDTest;

public static class BDTestUtil
{
    public static List<Scenario> GetScenarios() => GetScenarios(BDTestRun.Default);

    /// <summary>Gets a snapshot of the run's top-level scenarios.</summary>
    public static List<Scenario> GetScenarios(BDTestRun run) =>
        (run ?? throw new ArgumentNullException(nameof(run))).Store.ScenariosByInternalId.Values
        .Where(scenario => scenario.OutlineId == null).ToList();

    public static string CurrentReportId => BDTestRun.Default.ReportId;
    public static List<ScenarioDefinitionSummary> GetNotRunScenarios() => GetNotRunScenarios(BDTestRun.Default);

    /// <summary>Gets a snapshot of the run's unfinished scenario definitions.</summary>
    public static List<ScenarioDefinitionSummary> GetNotRunScenarios(BDTestRun run) =>
        (run ?? throw new ArgumentNullException(nameof(run))).Store.NotRun.Values.Select(definition => definition.ToSummary()).ToList();

    /// <summary>Clears recorded and not-run scenarios, pending output, and scenario listeners.</summary>
    /// <remarks>Call between test runs. This does not cancel executing tests or change the report ID.</remarks>
    public static void ClearScenarios() => ClearScenarios(BDTestRun.Default);

    /// <summary>Clears this run's scenarios, pending definitions, listeners, output, and temporary attachment files.</summary>
    /// <remarks>Call between executions. Does not cancel tests or change the report identifier.</remarks>
    public static void ClearScenarios(BDTestRun run) => (run ?? throw new ArgumentNullException(nameof(run))).Clear();

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
