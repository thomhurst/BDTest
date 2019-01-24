using System.Collections.Generic;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public class WarningsChecker
    {
        [JsonProperty]
        public IEnumerable<BuildableTest> NonExecutedTests;

        [JsonProperty]
        public IEnumerable<Scenario> StoppedEarlyTests;

        public WarningsChecker(IEnumerable<BuildableTest> nonExecutedTests, IEnumerable<Scenario> stoppedEarlyTests)
        {
            NonExecutedTests = nonExecutedTests;
            StoppedEarlyTests = stoppedEarlyTests;
        }

        [JsonConstructor]
        private WarningsChecker()
        {

        }
    }
}
