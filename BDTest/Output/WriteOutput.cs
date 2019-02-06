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
    public static class WriteOutput
    {
        private static bool _alreadyExecuted;
        private static readonly object Lock = new object();

        public static void OutputData(object sender, EventArgs e)
        {
            RunReportDll();
            Task.WaitAll(WriteWarnings());
        }

        internal static void Initialise()
        {
            lock (Lock)
            {
                if (_alreadyExecuted)
                {
                    return;
                }

                _alreadyExecuted = true;

                AppDomain.CurrentDomain.ProcessExit += OutputData;

                if (Directory.Exists(FileLocations.ScenariosDirectory))
                {
                    foreach (var filePath in Directory.GetFiles(FileLocations.ScenariosDirectory))
                    {
                        File.Delete(filePath);
                    }
                }

                Directory.CreateDirectory(FileLocations.ScenariosDirectory);

                if (File.Exists(FileLocations.Warnings))
                {
                    File.Delete(FileLocations.Warnings);
                }

                var runtimeConfigFile = Directory.GetFiles(FileLocations.OutputDirectory)
                    .FirstOrDefault(it => it.EndsWith(".runtimeconfig.dev.json"));

                if (runtimeConfigFile == null)
                {
                    return;
                }

                var bdTestReportRunConfigPath = Path.Combine(FileLocations.OutputDirectory,
                    "BDTest.ReportGenerator.runtimeconfig.dev.json");

                if (File.Exists(bdTestReportRunConfigPath))
                {
                    File.Delete(bdTestReportRunConfigPath);
                }

                File.Copy(runtimeConfigFile,
                    bdTestReportRunConfigPath);

                if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt")))
                {
                    File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt"));
                }

                if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Exception.txt")))
                {
                    File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Exception.txt"));
                }

                if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Report Exception.txt")))
                {
                    File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Report Exception.txt"));
                }
            }
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

        private static void RunReportDll()
        {
                try
                {
                    var dllArguments = GetDllArguments();

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
                    File.WriteAllText(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Exception.txt"), e.StackTrace);
                }
        }

        public static string GetDllArguments()
        {
            var reportDll = Directory.CreateDirectory(FileLocations.OutputDirectory).GetFiles("BDTest.ReportGenerator.dll")
                .FirstOrDefault()?.FullName;

            return 
                $"\"{reportDll}\" \"{Arguments.ResultDirectoryArgumentName}{FileLocations.OutputDirectory}\" \"{Arguments.PersistentStorageArgumentName}{BDTestSettings.PersistentResultsDirectory}\" \"{Arguments.PersistentResultsCompareStartTimeArgumentName}{BDTestSettings.PersistentResultsCompareStartTime:o}\" \"{Arguments.AllScenariosReportHtmlFilenameArgumentName}{BDTestSettings.AllScenariosReportHtmlFilename}\" \"{Arguments.ScenariosByStoryReportHtmlFilenameArgumentName}{BDTestSettings.ScenariosByStoryReportHtmlFilename}\" \"{Arguments.FlakinessReportHtmlFilenameArgumentName}{BDTestSettings.FlakinessReportHtmlFilename}\" \"{Arguments.TestTimesReportHtmlFilenameArgumentName}{BDTestSettings.TestTimesReportHtmlFilename}\" \"{Arguments.JsonDataFilenameArgumentName}{BDTestSettings.JsonDataFilename}\" \"{Arguments.XmlDataFilenameArgumentName}{BDTestSettings.XmlDataFilename}\" ";
        }
    }
}