using BDTest.Output;
using BDTest.Test;
using System.Text.Json.Serialization;

namespace BDTest.Maps;

public class BDTestOutputModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
        
    [JsonInclude]
    public string Environment { get; set; }
        
    [JsonInclude]
    public string Tag { get; set; }
        
    [JsonInclude]
    public string BranchName { get; set; }
        
    [JsonInclude]
    public string MachineName { get; set; }
        
    [JsonInclude]
    public TestTimer TestTimer { get; set; }
        
    [JsonInclude]
    public List<Scenario> Scenarios { get; set; } = new();
        
    [JsonInclude]
    public List<BuildableTest> NotRun { get; set; } = new();

    [JsonInclude]
    public string Version { get; set; }
        
    [JsonInclude]
    public Dictionary<string, string> CustomProperties { get; internal set; } = new();
}