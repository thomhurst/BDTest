using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>Report metadata for a definition that has not run; never contains executable code.</summary>
public sealed class ScenarioDefinitionSummary
{
    public string ScenarioId { get; }
    public string ReportId { get; }
    public string StoryText { get; }
    public string ScenarioText { get; }
    public string? TestId { get; }
    public string? CallerMember { get; }
    public string? CallerFile { get; }
    public IReadOnlyList<string> Parameters { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<string> CustomTestInformation { get; }
    public TimeSpan? Timeout { get; }
    public IReadOnlyList<StepDefinitionSummary> Steps { get; }

    [JsonConstructor]
    public ScenarioDefinitionSummary(
        string scenarioId,
        string reportId,
        string storyText,
        string scenarioText,
        string? testId = null,
        string? callerMember = null,
        string? callerFile = null,
        IReadOnlyList<string>? parameters = null,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? customTestInformation = null,
        TimeSpan? timeout = null,
        IReadOnlyList<StepDefinitionSummary>? steps = null)
    {
        ScenarioId = scenarioId ?? throw new ArgumentNullException(nameof(scenarioId));
        ReportId = reportId ?? throw new ArgumentNullException(nameof(reportId));
        StoryText = storyText ?? throw new ArgumentNullException(nameof(storyText));
        ScenarioText = scenarioText ?? throw new ArgumentNullException(nameof(scenarioText));
        TestId = testId;
        CallerMember = callerMember;
        CallerFile = callerFile;
        Parameters = ContractCollections.List(parameters);
        Tags = ContractCollections.List(tags);
        CustomTestInformation = ContractCollections.List(customTestInformation);
        Timeout = timeout;
        Steps = ContractCollections.List(steps);
    }
}
