using System.Collections.Generic;
using BDTest.Maps;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public class WarningsChecker
    {
        [JsonProperty]
        public IEnumerable<BuildableTest> NonExecutedTests;

        public WarningsChecker(IEnumerable<BuildableTest> nonExecutedTests)
        {
            NonExecutedTests = nonExecutedTests;
        }

        [JsonConstructor]
        private WarningsChecker()
        {

        }
    }
}
