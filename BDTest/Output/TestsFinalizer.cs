using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.Paths;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public static class TestsFinalizer
    {

        internal static bool Debug { get; set; }

        static TestsFinalizer()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Task.WaitAll(RunReportDll(), WriteWarnings(), Wait());
        }

        private static async Task Wait()
        {
            await Task.Delay(2000);
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
                    var warnings = new WarningsChecker(TestMap.NotRun.Values, TestMap.StoppedEarly.Values);
                    File.WriteAllText(FileLocations.Warnings, JsonConvert.SerializeObject(warnings, settings));
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt"), e.StackTrace);
                }
            });
        }

        private static async Task RunReportDll()
        {
            await Task.Run(() =>
            {
                try
                {
                    var dllArguments = GetDllArguments();

                    if (string.IsNullOrEmpty(dllArguments))
                    {
                        return;
                    }

                    if (Debug)
                    {
                        File.WriteAllText(Path.Combine(FileLocations.OutputDirectory, "BDTest - Debug.txt"), dllArguments);
                    }

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = dllArguments,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                            CreateNoWindow = true
                        }

                    };

                    process.Start();
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Report Exception.txt"),
                        e.StackTrace);
                }
            });
        }

        public static string GetDllArguments()
        {
            var reportDll = Directory.CreateDirectory(FileLocations.OutputDirectory).GetFiles("BDTest.ReportGenerator.dll")
                .FirstOrDefault()?.FullName;

            if (string.IsNullOrEmpty(reportDll))
            {
                return null;
            }

            return 
                $"\"{reportDll}\" \"{Arguments.ResultDirectoryArgumentName}{FileLocations.OutputDirectory}\" \"{Arguments.PersistentStorageArgumentName}{BDTestSettings.PersistentResultsDirectory}\" \"{Arguments.PersistentResultsCompareStartTimeArgumentName}{BDTestSettings.PersistentResultsCompareStartTime:o}\" \"{Arguments.AllScenariosReportHtmlFilenameArgumentName}{BDTestSettings.AllScenariosReportHtmlFilename}\" \"{Arguments.ScenariosByStoryReportHtmlFilenameArgumentName}{BDTestSettings.ScenariosByStoryReportHtmlFilename}\" \"{Arguments.FlakinessReportHtmlFilenameArgumentName}{BDTestSettings.FlakinessReportHtmlFilename}\" \"{Arguments.TestTimesReportHtmlFilenameArgumentName}{BDTestSettings.TestTimesReportHtmlFilename}\" \"{Arguments.JsonDataFilenameArgumentName}{BDTestSettings.JsonDataFilename}\" \"{Arguments.XmlDataFilenameArgumentName}{BDTestSettings.XmlDataFilename}\" ";
        }
    }
}