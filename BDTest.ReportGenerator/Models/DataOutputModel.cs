using System.Collections.Generic;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.Models
{
    public class DataOutputModel
    {
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
