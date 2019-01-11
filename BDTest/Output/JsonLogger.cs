using System;
using System.IO;
using Newtonsoft.Json;
using BDTest.Paths;
using BDTest.Test;

namespace BDTest.Output
{

    internal static class JsonLogger
    {
        static JsonLogger()
        {

            if (Directory.Exists(FileLocations.ScenariosDirectory))
            {
                foreach (var filePath in Directory.GetFiles(FileLocations.ScenariosDirectory))
                {
                    File.Delete(filePath);
                }
            }

            Directory.CreateDirectory(FileLocations.ScenariosDirectory);

            AppDomain.CurrentDomain.ProcessExit += OutputData;
        }

        public static void OutputData(object sender, EventArgs e)
        {
            WriteOutput.OutputData();
        }

        internal static void WriteScenario(Scenario scenario)
        {
            File.WriteAllText(FileLocations.RandomScenarioFilePath, JsonConvert.SerializeObject(scenario, Formatting.Indented));
        }
    }
}
