using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Metadata;
using System.Text.Json.Serialization;

namespace BDTest.Test;

public class BuildableTest
{
    [JsonIgnore] internal TestMethodMetadata TestMetadata { get; set; }

    /// <summary>Gets the run captured when this scenario definition was built.</summary>
    [JsonIgnore]
    public BDTestRun Run { get; internal set; } = BDTestRun.Default;

    [JsonIgnore]
    internal TimeSpan? ScenarioTimeout { get; set; }

    [JsonIgnore]
    internal string Guid { get; set; }

    [JsonInclude]
    internal StoryText StoryText { get; set; }

    [JsonInclude]
    internal ScenarioText ScenarioText { get; set; }
        
    [JsonInclude]
    public string ScenarioId { get; internal set; }
        
    [JsonInclude]
    public string ReportId { get; internal set; }

    [JsonInclude]
    public IEnumerable<string> Parameters { get; protected internal set; } = Enumerable.Empty<string>();

    [JsonInclude]
    public TestInformationAttribute[] CustomTestInformation { get; protected internal set; } = Array.Empty<TestInformationAttribute>();

    /// <summary>Combined story and scenario tags, distinct ignoring case.</summary>
    [JsonInclude]
    public string[] Tags { get; protected internal set; } = Array.Empty<string>();
        
    [JsonInclude]
    public string CallerMember { get; protected internal set; }
    [JsonInclude]
    public string CallerFile { get; protected internal set; }
    [JsonInclude]
    public string TestId { get; protected internal set; }
    [JsonIgnore]
    public BDTestBase BdTestBase { get; protected set; }

    public string GetScenarioText()
    {
        return string.IsNullOrWhiteSpace(ScenarioText?.Scenario) ? DefaultValues.ScenarioTextNotDefined : ScenarioText.Scenario;
    }

    public string GetStoryText()
    {
        return string.IsNullOrWhiteSpace(StoryText?.Story) ? DefaultValues.StoryTextNotDefined : StoryText.Story;
    }
}
