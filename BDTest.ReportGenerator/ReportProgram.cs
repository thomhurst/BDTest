using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BDTest.Output;
using BDTest.Paths;
using BDTest.ReportGenerator.Builders;
using BDTest.ReportGenerator.Models;
using BDTest.ReportGenerator.Utils;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator
{
    internal static class ReportProgram
    {
        public static string ResultDirectory { get; private set; }
        public static string Args { get; private set; }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, "BDTest - Report Exception.txt"), "Args: " + Args + Environment.NewLine + (e.ExceptionObject as Exception)?.StackTrace);
        }

        public static void Main(string[] args)
        {
            Args = string.Join(" ", args);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ResultDirectory = args.FirstOrDefault(it => it.StartsWith(WriteOutput.ResultDirectoryArgumentName))?.Replace(WriteOutput.ResultDirectoryArgumentName, "");

            SetSettingsFromArgs(args);

            if (ResultDirectory == null)
            {
                return;
            }

            var scenarios = GetScenarios(ResultDirectory);
            var testTimer = GetTestTimer(scenarios);

            var reportPathByStory = Path.Combine(ResultDirectory, FileNames.ReportByStory);
            var reportPathAllScenarios = Path.Combine(ResultDirectory, FileNames.ReportAllScenarios);
            var testDataJsonPath = Path.Combine(ResultDirectory, FileNames.TestDataJson);
            var testDataXmlPath = Path.Combine(ResultDirectory, FileNames.TestDataXml);

            DeleteExistingFiles(reportPathByStory, reportPathAllScenarios, testDataJsonPath, testDataXmlPath);

            var warnings = GetWarnings();

            scenarios.AddRange(warnings.StoppedEarlyTests);

            var dataToOutput = new DataOutputModel
            {
                Scenarios = scenarios,
                TestTimer = testTimer,
                Warnings = warnings
            };

            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            var jsonData = JsonConvert.SerializeObject(dataToOutput, Formatting.Indented, settings);


            WriteJsonOutput(jsonData);

            WriteXmlOutput(jsonData);

            HtmlReportBuilder.CreateReport(dataToOutput);

            try
            {
                CopyFolder.Copy(Path.Combine(FileLocations.ProjectDirectory, "css"), Path.Combine(ResultDirectory, "css"));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static void SetSettingsFromArgs(string[] args)
        {
            BDTestSettings.PersistentResultsDirectory = GetArgument(args, WriteOutput.PersistentStorageArgumentName);

            var persistentCompareStartDate = GetArgument(args, WriteOutput.PersistentResultsCompareStartTimeArgumentName);
            if (persistentCompareStartDate != null)
            {
                BDTestSettings.PersistentResultsCompareStartTime = DateTime.ParseExact(persistentCompareStartDate, "o", CultureInfo.InvariantCulture);
            }

            BDTestSettings.AllScenariosReportHtmlFilename = GetArgument(args, WriteOutput.AllScenariosReportHtmlFilenameArgumentName);

            BDTestSettings.ScenariosByStoryReportHtmlFilename = GetArgument(args, WriteOutput.ScenariosByStoryReportHtmlFilenameArgumentName);

            BDTestSettings.FlakinessReportHtmlFilename = GetArgument(args, WriteOutput.FlakinessReportHtmlFilenameArgumentName);

            BDTestSettings.TestTimesReportHtmlFilename = GetArgument(args, WriteOutput.TestTimesReportHtmlFilenameArgumentName);

            BDTestSettings.XmlDataFilename = GetArgument(args, WriteOutput.XmlDataFilenameArgumentName);

            BDTestSettings.JsonDataFilename = GetArgument(args, WriteOutput.JsonDataFilenameArgumentName);

            if (string.IsNullOrWhiteSpace(BDTestSettings.PersistentResultsDirectory)) return;

            try
            {
                Directory.CreateDirectory(BDTestSettings.PersistentResultsDirectory);
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(ResultDirectory, "BDTest - Persistent Directory Error.txt"), e.StackTrace);
                BDTestSettings.PersistentResultsDirectory = null;
            }
        }

        private static string GetArgument(IEnumerable<string> args, string argumentName)
        {
            var argument = args.FirstOrDefault(it => it.StartsWith(argumentName))?.Replace(argumentName, "");

            return string.IsNullOrWhiteSpace(argument) ? null : argument;
        }

        private static void WriteJsonOutput(string jsonData)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, BDTestSettings.JsonDataFilename ?? FileNames.TestDataJson), jsonData);

            if (!string.IsNullOrWhiteSpace(BDTestSettings.PersistentResultsDirectory))
            {
                File.Copy(Path.Combine(ResultDirectory, BDTestSettings.JsonDataFilename ?? FileNames.TestDataJson),
                    Path.Combine(BDTestSettings.PersistentResultsDirectory, FileNames.TestDataJson));
            }
        }

        private static void WriteXmlOutput(string jsonData)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, BDTestSettings.XmlDataFilename ?? FileNames.TestDataXml),
                JsonConvert.DeserializeXmlNode(jsonData, "TestData").ToXmlString());
        }

        private static void DeleteExistingFiles(params string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                DeleteExistingFile(filePath);
            }
        }

        private static void DeleteExistingFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static List<Scenario> GetScenarios(string resultDirectory)
        {
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                Error = (se, ev) => { ev.ErrorContext.Handled = true; }
            };

            var scenariosFolder = Directory.GetDirectories(resultDirectory, FileNames.Scenarios).FirstOrDefault();

            if (scenariosFolder == null)
            {
                throw new ArgumentNullException(nameof(scenariosFolder), $"Can't find '{FileNames.Scenarios} folder in directory {resultDirectory}");
            }

            var scenarios = Directory.GetFiles(scenariosFolder).Select(scenarioFile =>
                JsonConvert.DeserializeObject<Scenario>(File.ReadAllText(scenarioFile), settings));

            return scenarios.ToList();
        }

        private static WarningsChecker GetWarnings()
        {
            var warningsPath = Path.Combine(ResultDirectory, FileNames.Warnings);

            if (!File.Exists(warningsPath))
            {
                Thread.Sleep(2000);
            }

            return !File.Exists(warningsPath) ? new WarningsChecker(new List<BuildableTest>(), new List<Scenario>()) : JsonConvert.DeserializeObject<WarningsChecker>(File.ReadAllText(warningsPath));
        }

        private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios)
        {
            var enumerable = scenarios.ToList();

            if (enumerable.Count == 0)
            {
                return new TestTimer();
            }

            var testTimer = new TestTimer
            {
                TestsStartedAt = enumerable.GetStartDateTime(),
                TestsFinishedAt = enumerable.GetEndDateTime()
            };
            testTimer.ElapsedTime = testTimer.TestsFinishedAt - testTimer.TestsStartedAt;

            return testTimer;
        }
    }
}
