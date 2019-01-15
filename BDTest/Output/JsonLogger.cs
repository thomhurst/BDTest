using System;
using System.IO;
using Newtonsoft.Json;
using BDTest.Paths;
using BDTest.Test;

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
