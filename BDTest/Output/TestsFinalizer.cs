using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.Paths;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public static class TestsFinalizer
    {
        internal static void Initialise()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Task.WaitAll(WriteWarnings());
        }

        private static async Task WriteWarnings()
        {
            await Task.Run(() =>
            {
                var settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                };

                try
                {
                    var warnings = new WarningsChecker(TestHolder.NotRun.Values, TestHolder.StoppedEarly.Values);
                    File.WriteAllText(FileLocations.Warnings, JsonConvert.SerializeObject(warnings, settings));
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt"), e.StackTrace);
                }
            });
        }
    }
}