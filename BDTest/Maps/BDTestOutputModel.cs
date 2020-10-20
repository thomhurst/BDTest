using System.Collections.Generic;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Maps
{
    public class BDTestOutputModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty]
        public string Environment { get; set; }
        
        [JsonProperty]
        public string Tag { get; set; }
        
        [JsonProperty]
        public string BranchName { get; set; }
        
        [JsonProperty]
        public string MachineName { get; set; }
        
        [JsonProperty]
        public TestTimer TestTimer { get; set; }
        
        [JsonProperty]
        public List<Scenario> Scenarios { get; set; } = new List<Scenario>();
        
        [JsonProperty]
        public List<BuildableTest> NotRun { get; set; } = new List<BuildableTest>();

        [JsonProperty] 
        public string Version { get; set; }
        
        [JsonProperty]
        public Dictionary<string, string> CustomProperties { get; private set; } = new Dictionary<string, string>();
    }
}
