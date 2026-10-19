using BDTest.Contracts;

namespace BDTest.Razor.Reports.Extensions;

public static class ScenarioExtensions
{
    public static Status GetTotalStatus(this IEnumerable<ScenarioResult> scenarios)
    {
        var scenariosList = scenarios.ToList();
        if (scenariosList.Count > 0 && scenariosList.All(scenario => scenario.Status == Status.Skipped))
        {
            return Status.Skipped;
        }

        // Skips do not override completed passes, but unfinished results do.
        return scenariosList.Where(scenario => scenario.Status != Status.Skipped)
            .OrderBy(scenario => scenario.Status.GetOrder()).FirstOrDefault()?.Status ?? Status.NotRun;
    }

    public static string GetCssColourValueForStatus(this Status status)
    {
        switch (status)
        {
            case Status.Passed:
                return "btn-success";
            case Status.Failed:
                return "btn-danger";
            case Status.NotRun:
            case Status.Pending:
                return "btn-secondary";
            case Status.Inconclusive:
            case Status.NotImplemented:
            case Status.Skipped:
                return "btn-warning";
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    public static string AsString(this Status status)
    {
        switch (status)
        {
            case Status.NotRun:
                return "Not run";
            case Status.Pending:
            case Status.Passed:
            case Status.Failed:
            case Status.Inconclusive:
            case Status.Skipped:
                return status.ToString();
            case Status.NotImplemented:
                return "Not Implemented";
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}