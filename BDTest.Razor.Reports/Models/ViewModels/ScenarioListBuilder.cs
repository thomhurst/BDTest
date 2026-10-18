using BDTest.Razor.Reports.Constants;
using BDTest.Test;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Models.ViewModels;

internal static class ScenarioListBuilder
{
    public static ScenarioListViewModel Build(IEnumerable<IGrouping<string, Scenario>> groups, IReadOnlyList<Scenario> reportScenarios,
        string reportId, HttpRequest request, bool groupFixed)
    {
        var query = request.Query;
        var tag = query[TagConstants.FilterByTagQueryParameterName].ToString();
        var search = query[ScenarioSearch.QueryParameterName].ToString().Trim();
        var groupBy = groupFixed || query["group"] != ScenarioListViewModel.GroupByNone
            ? ScenarioListViewModel.GroupByStory
            : ScenarioListViewModel.GroupByNone;

        var scenarios = groups.SelectMany(group => group).Where(scenario => scenario != null).ToList();
        if (groupBy == ScenarioListViewModel.GroupByStory)
        {
            // Stories with problems first, then by name; scenarios keep their order within a story.
            scenarios = scenarios.GroupBy(scenario => scenario.GetStoryText())
                .OrderBy(story => Extensions.ScenarioExtensions.GetTotalStatus(story).GetOrder())
                .ThenBy(story => story.Key, StringComparer.Ordinal)
                .SelectMany(story => story)
                .ToList();
        }

        var unfiltered = reportScenarios
            .Where(scenario => string.IsNullOrEmpty(tag) || scenario.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Where(scenario => ScenarioSearch.Matches(scenario, search))
            .ToList();

        var selectedId = query["scenarioId"].ToString();

        return new ScenarioListViewModel
        {
            ReportId = reportId,
            Scenarios = scenarios,
            Unfiltered = unfiltered,
            Total = reportScenarios.Count,
            GroupBy = groupBy,
            GroupFixed = groupFixed,
            Statuses = ParseStatuses(query[StatusConstants.FilterByStatusQueryParameterName]),
            Tags = reportScenarios.SelectMany(scenario => scenario.Tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Selected = string.IsNullOrEmpty(selectedId) ? null : scenarios.FirstOrDefault(scenario => scenario.Guid == selectedId)
        };
    }

    public static IReadOnlySet<Status> ParseStatuses(string value)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value, StatusConstants.All, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<Status>(part, true, out var status) && Enum.IsDefined(status) ? (Status?)status : null)
            .Where(status => status != null)
            .Select(status => status.Value)
            .ToHashSet();
    }
}
