using BDTest.Attributes;
using BDTest.Constants;
using Newtonsoft.Json;

namespace BDTest.Test;

public class BuildableTest
{
    [JsonIgnore]
    internal string Guid { get; set; }

    [JsonProperty] 
    internal StoryText StoryText { get; set; }

    [JsonProperty]
    internal ScenarioText ScenarioText { get; set; }
        
    [JsonProperty]
    public string ScenarioId { get; internal set; }
        
    [JsonProperty]
    public string ReportId { get; internal set; }

    [JsonProperty] 
    public IEnumerable<string> Parameters { get; protected set; } = Enumerable.Empty<string>();

    [JsonProperty]
    public TestInformationAttribute[] CustomTestInformation { get; protected set; } = Array.Empty<TestInformationAttribute>();
        
    [JsonProperty]
    public string CallerMember { get; protected set; }
    [JsonProperty]
    public string CallerFile { get; protected set; }
    [JsonProperty]
    public string TestId { get; protected set; }
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