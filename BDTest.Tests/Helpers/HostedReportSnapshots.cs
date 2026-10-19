using BDTest.Contracts;
using BDTest.Maps;
using BDTest.Razor.Reports.Serialization;

namespace BDTest.Tests;

// Existing runtime/legacy fixtures are projected only at the host boundary.
internal static class HostedReportSnapshots
{
    internal static BDTestReport ToReport(this BDTestOutputModel report) =>
        HostedReportJson.Deserialize(BDTest.Helpers.BDTestJsonHelper.Serialize(report))
        ?? throw new InvalidOperationException("Expected a report.");

    internal static ScenarioResult ToResult(this Test.Scenario scenario) => BDTest.Helpers.ResultSnapshot.Create(scenario);
    internal static StepResult ToResult(this Test.Step step) => BDTest.Helpers.ResultSnapshot.Create(step);
    internal static ExceptionInfo? ToResult(this Test.ExceptionWrapper? exception) => BDTest.Helpers.ResultSnapshot.Create(exception);
    internal static IReadOnlyList<ScenarioResult> ToResults(this IEnumerable<Test.Scenario> scenarios) => scenarios.Select(ToResult).ToArray();
}
