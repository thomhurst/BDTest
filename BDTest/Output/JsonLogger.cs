using System.IO;
using BDTest.Paths;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Output
{

    internal static class JsonLogger
    {
        internal static void WriteScenario(Scenario scenario)
        {
            File.WriteAllText(FileLocations.RandomScenarioFilePath, JsonConvert.SerializeObject(scenario, Formatting.Indented));
        }
    }
}
