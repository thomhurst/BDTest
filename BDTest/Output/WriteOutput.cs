using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BDTest.Maps;
using BDTest.Paths;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public static class WriteOutput
    {
        public static string ResultDirectoryArgumentName { get; } = "-ResultsDirectory=";
        public static string PersistentStorageArgumentName { get; } = "-PersistentStorageDirectory=";
        public static string PersistentResultsCompareStartTimeArgumentName { get; } = "-PersistentResultsStartCompareTime=";
        public static string ScenariosByStoryReportHtmlFilenameArgumentName { get; } = "-ScenariosByStoryReportHtmlFilename=";
        public static string AllScenariosReportHtmlFilenameArgumentName { get; } = "-AllScenariosReportHtmlFilename=";
        public static string FlakinessReportHtmlFilenameArgumentName { get; } = "-FlakinessReportHtmlFilename=";
        public static string TestTimesReportHtmlFilenameArgumentName { get; } = "-TestTimesReportHtmlFilename=";
        public static string JsonDataFilenameArgumentName { get; } = "-JsonDataFilename=";
        public static string XmlDataFilenameArgumentName { get; } = "-XmlDataFilename=";


        public static string OutputDirectory { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static bool _alreadyExecuted;
        private static readonly object Lock = new object();

        static WriteOutput()
        {
            AppDomain.CurrentDomain.ProcessExit += OutputData;
        }

        public static void OutputData(object sender, EventArgs e)
        {
            Task.WaitAll(WriteWarnings(), RunReportDll());
        }

        public static void Initialise()
        {
            lock (Lock)
            {
                if (_alreadyExecuted)
                {
                    return;
                }

                _alreadyExecuted = true;

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

                if (File.Exists(Path.Combine(OutputDirectory, "BDTest - Exception.txt")))
                {
                    File.Delete(Path.Combine(OutputDirectory, "BDTest - Exception.txt"));
                }

                if (File.Exists(Path.Combine(OutputDirectory, "BDTest - Run Exception.txt")))
                {
                    File.Delete(Path.Combine(OutputDirectory, "BDTest - Run Exception.txt"));
                }

                if (File.Exists(Path.Combine(OutputDirectory, "BDTest - Report Exception.txt")))
                {
                    File.Delete(Path.Combine(OutputDirectory, "BDTest - Report Exception.txt"));
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
                    File.WriteAllText(Path.Combine(OutputDirectory, "BDTest - Exception.txt"), e.StackTrace);
                }
            });
        }

        private static async Task RunReportDll()
        {
            await Task.Run(() =>
            {
                try
                {
                    var reportDll = Directory.CreateDirectory(OutputDirectory).GetFiles("BDTest.ReportGenerator.dll")
                        .FirstOrDefault()?.FullName;

                    if (OutputDirectory == null || reportDll == null)
                    {
                        return;
                    }

                    var dllArguments = $"\"{reportDll}\" \"{ResultDirectoryArgumentName}{OutputDirectory}\" \"{PersistentStorageArgumentName}{BDTestSettings.PersistentResultsDirectory}\" \"{PersistentResultsCompareStartTimeArgumentName}{BDTestSettings.PersistentResultsCompareStartTime:o}\" \"{AllScenariosReportHtmlFilenameArgumentName}{BDTestSettings.AllScenariosReportHtmlFilename}\" \"{ScenariosByStoryReportHtmlFilenameArgumentName}{BDTestSettings.ScenariosByStoryReportHtmlFilename}\" \"{FlakinessReportHtmlFilenameArgumentName}{BDTestSettings.FlakinessReportHtmlFilename}\" \"{TestTimesReportHtmlFilenameArgumentName}{BDTestSettings.TestTimesReportHtmlFilename}\" \"{JsonDataFilenameArgumentName}{BDTestSettings.JsonDataFilename}\" \"{XmlDataFilenameArgumentName}{BDTestSettings.XmlDataFilename}\" ";

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
                    File.WriteAllText(Path.Combine(OutputDirectory, "BDTest - Run Exception.txt"), e.StackTrace);
                }
            });
        }
    }
}