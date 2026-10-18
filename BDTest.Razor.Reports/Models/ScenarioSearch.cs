using BDTest.Test;

namespace BDTest.Razor.Reports.Models;

/// <summary>Free-text search over scenario titles, stories, steps, tags and exceptions.</summary>
public static class ScenarioSearch
{
    public const string QueryParameterName = "q";
    public const string StoryQueryParameterName = "story";

    public static bool Matches(Scenario? scenario, string? search)
    {
        if (scenario == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        bool Has(string? text) => text?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

        return Has(scenario.GetScenarioText())
               || Has(scenario.GetStoryText())
               || scenario.Tags.Any(Has)
               || Has(scenario.Exception?.TypeName)
               || Has(scenario.Exception?.Message)
               || (scenario.Steps ?? new List<Step>()).Any(step => Has(step.StepText))
               || (scenario.Examples ?? new List<Scenario>()).Any(example => Matches(example, search));
    }
}
