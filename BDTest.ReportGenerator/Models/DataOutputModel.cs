using System.Collections.Generic;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BDTest.ReportGenerator.Models
{
    internal class DataOutputModel
    {
        [JsonProperty]
        public TestTimer TestTimer;
        [JsonProperty]
        public List<Scenario> Scenarios;
        [JsonProperty]
        public WarningsChecker Warnings;
    }
}
