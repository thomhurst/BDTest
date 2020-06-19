using System.Collections.Generic;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Maps
{
    public class BDTestOutputModel
    {
        [JsonProperty]
        public string Guid { get; set; }
        
        [JsonProperty]
        public TestTimer TestTimer { get; set; }
        
        [JsonProperty]
        public List<Scenario> Scenarios { get; set; }
        
        [JsonProperty]
        public List<BuildableTest> NotRun { get; set; }

        [JsonProperty] 
        public string Version { get; set; }
    }
}
