using BDTest.Test;
using BDTest.Metadata;

namespace BDTest;

/// <summary>Fluent construction state. Report JSON contains definition summaries, never builders.</summary>
public class BuildableTest
{
    internal ScenarioDefinition Definition { get; set; } = new();

    internal TestMethodMetadata? TestMetadata { get => Definition.TestMetadata; set => Definition.TestMetadata = value; }

    /// <summary>Gets the run captured when this scenario definition was built.</summary>
    public BDTestRun Run { get => Definition.Run; internal set => Definition.Run = value; }
    internal TimeSpan? ScenarioTimeout { get => Definition.ScenarioTimeout; set => Definition.ScenarioTimeout = value; }
    internal string? Guid { get => Definition.Guid; set => Definition.Guid = value; }
    internal StoryText? StoryText { get => Definition.StoryText; set => Definition.StoryText = value; }
    internal ScenarioText? ScenarioText { get => Definition.ScenarioText; set => Definition.ScenarioText = value; }
    public string? ScenarioId { get => Definition.Guid; internal set => Definition.Guid = value; }
    public string? ReportId { get => Definition.ReportId; internal set => Definition.ReportId = value; }
    /// <summary>Names of the parameters on the test method, not their argument values.</summary>
    public IEnumerable<string?>? ParameterNames { get => Definition.ParameterNames; protected internal set => Definition.ParameterNames = value; }
    public TestInformationAttribute[]? CustomTestInformation { get => Definition.CustomTestInformation; protected internal set => Definition.CustomTestInformation = value; }

    /// <summary>Combined story and scenario tags, distinct ignoring case.</summary>
    public string[]? Tags { get => Definition.Tags; protected internal set => Definition.Tags = value; }
    public string? CallerMember { get => Definition.CallerMember; protected internal set => Definition.CallerMember = value; }
    public string? CallerFile { get => Definition.CallerFile; protected internal set => Definition.CallerFile = value; }
    public string? TestId { get => Definition.TestId; protected internal set => Definition.TestId = value; }
    public BDTestBase? BdTestBase { get => Definition.BdTestBase; protected set => Definition.BdTestBase = value; }

    public string GetScenarioText() => Definition.GetScenarioText();

    public string GetStoryText() => Definition.GetStoryText();
}
