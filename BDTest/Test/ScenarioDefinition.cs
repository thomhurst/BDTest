using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Metadata;
using BDTest.Test.Steps;

namespace BDTest.Test;

// Reusable construction data. Copy configuration before execution or branching;
// step outcomes never belong to a definition.
internal sealed class ScenarioDefinition
{
    internal TestMethodMetadata? TestMetadata { get; set; }
    internal BDTestRun Run { get; set; } = BDTestRun.Default;
    internal TimeSpan? ScenarioTimeout { get; set; }
    internal string? Guid { get; set; }
    internal StoryText? StoryText { get; set; }
    internal ScenarioText? ScenarioText { get; set; }
    internal string? ReportId { get; set; }
    internal IEnumerable<string?>? ParameterNames { get; set; } = Array.Empty<string>();
    internal TestInformationAttribute[]? CustomTestInformation { get; set; } = Array.Empty<TestInformationAttribute>();
    internal string[]? Tags { get; set; } = Array.Empty<string>();
    internal string? CallerMember { get; set; }
    internal string? CallerFile { get; set; }
    internal string? TestId { get; set; }
    internal BDTestBase? BdTestBase { get; set; }
    internal List<StepDefinition> Steps { get; private set; } = new();

    internal ScenarioDefinition Snapshot()
    {
        var copy = (ScenarioDefinition)MemberwiseClone();
        copy.ParameterNames = ParameterNames?.ToArray();
        copy.Tags = (string[]?)Tags?.Clone();
        // Do not run user attribute constructors again when copying a chain.
        copy.CustomTestInformation = (TestInformationAttribute[]?)CustomTestInformation?.Clone();
        copy.Steps = Steps.Select(step => step.Snapshot()).ToList();
        return copy;
    }

    internal string GetScenarioText() => ScenarioText?.Scenario == null || string.IsNullOrWhiteSpace(ScenarioText.Scenario)
        ? DefaultValues.ScenarioTextNotDefined : ScenarioText.Scenario;
    internal string GetStoryText() => StoryText?.Story == null || string.IsNullOrWhiteSpace(StoryText.Story)
        ? DefaultValues.StoryTextNotDefined : StoryText.Story;

    internal ScenarioDefinitionSummary ToSummary() => new()
    {
        ScenarioId = Guid, ReportId = ReportId, StoryText = StoryText, ScenarioText = ScenarioText,
        ParameterNames = ParameterNames?.ToArray(), Tags = (string[]?)Tags?.Clone(),
        // Pending reports retain only data, never arbitrary user attribute objects.
        CustomTestInformation = CustomTestInformation?.Select(item => new TestInformationAttribute(item.Information)).ToArray(),
        CallerMember = CallerMember, CallerFile = CallerFile, TestId = TestId, Timeout = ScenarioTimeout,
        Steps = Steps.Select(step => new StepDefinitionSummary
        {
            StepType = step.StepType, StepText = $"{step.StepType.GetValue()} {step.TextTemplate}", Timeout = step.Timeout
        }).ToArray()
    };
}
