using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Test.Steps;
using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>A report snapshot of a chain that has not started, without delegates or a test instance.</summary>
public sealed class ScenarioDefinitionSummary
{
    [JsonInclude] internal StoryText? StoryText { get; set; }
    [JsonInclude] internal ScenarioText? ScenarioText { get; set; }
    [JsonInclude] public string? ScenarioId { get; internal set; }
    [JsonInclude] public string? ReportId { get; internal set; }
    /// <summary>Names of the parameters on the test method, not their argument values.</summary>
    [JsonInclude, JsonPropertyName("Parameters")] public IEnumerable<string?>? ParameterNames { get; internal set; } = Array.Empty<string>();
    [JsonInclude] public TestInformationAttribute[]? CustomTestInformation { get; internal set; } = Array.Empty<TestInformationAttribute>();
    [JsonInclude] public string[]? Tags { get; internal set; } = Array.Empty<string>();
    [JsonInclude] public string? CallerMember { get; internal set; }
    [JsonInclude] public string? CallerFile { get; internal set; }
    [JsonInclude] public string? TestId { get; internal set; }
    [JsonInclude] public TimeSpan? Timeout { get; internal set; }
    [JsonInclude] public IReadOnlyList<StepDefinitionSummary>? Steps { get; internal set; } = Array.Empty<StepDefinitionSummary>();

    public string GetScenarioText() => ScenarioText?.Scenario == null || string.IsNullOrWhiteSpace(ScenarioText.Scenario)
        ? DefaultValues.ScenarioTextNotDefined : ScenarioText.Scenario;
    public string GetStoryText() => StoryText?.Story == null || string.IsNullOrWhiteSpace(StoryText.Story)
        ? DefaultValues.StoryTextNotDefined : StoryText.Story;
}

/// <summary>Unevaluated step text and settings for a pending scenario.</summary>
public sealed class StepDefinitionSummary
{
    [JsonInclude, JsonConverter(typeof(JsonStringEnumConverter<StepType>))]
    public StepType StepType { get; internal set; }
    [JsonInclude] public string? StepText { get; internal set; }
    [JsonInclude] public TimeSpan? Timeout { get; internal set; }
}
