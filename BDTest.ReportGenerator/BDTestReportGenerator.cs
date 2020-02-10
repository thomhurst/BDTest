using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using BDTest.Output;
using BDTest.Paths;
using BDTest.ReportGenerator.Builders;
using BDTest.ReportGenerator.Models;
using BDTest.ReportGenerator.Utils;
using BDTest.Test;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace BDTest.ReportGenerator
{
    public static class BDTestReportGenerator
    {
        internal static string ResultDirectory { get; private set; }
        private static string Args { get; set; }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, "BDTest - Report Exception.txt"), "Args: " + Args + Environment.NewLine + (e.ExceptionObject as Exception)?.StackTrace);
        }

        public static void Generate()
        {
            var reportDll = Directory.CreateDirectory(FileLocations.OutputDirectory)
                .GetFiles("BDTest.ReportGenerator.dll")
                .FirstOrDefault()?.FullName;

            var command = new[]
            {
                reportDll, Arguments.ResultDirectoryArgumentName + FileLocations.OutputDirectory,
                Arguments.PersistentStorageArgumentName + BDTestSettings.PersistentResultsDirectory,
                Arguments.PersistentResultsCompareStartTimeArgumentName +
                $"{BDTestSettings.PersistentResultsCompareStartTime:o}",
                Arguments.AllScenariosReportHtmlFilenameArgumentName +
                BDTestSettings.AllScenariosReportHtmlFilename,
                Arguments.ScenariosByStoryReportHtmlFilenameArgumentName +
                BDTestSettings.ScenariosByStoryReportHtmlFilename,
                Arguments.FlakinessReportHtmlFilenameArgumentName + BDTestSettings.FlakinessReportHtmlFilename,
                Arguments.TestTimesReportHtmlFilenameArgumentName + BDTestSettings.TestTimesReportHtmlFilename,
                Arguments.JsonDataFilenameArgumentName + BDTestSettings.JsonDataFilename,
                Arguments.XmlDataFilenameArgumentName + BDTestSettings.XmlDataFilename
            };

            Main(command);
        }

        internal static void Main(string[] args)
        {
            Args = string.Join(" ", args);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ResultDirectory = args.FirstOrDefault(it => it.StartsWith(Arguments.ResultDirectoryArgumentName))?.Replace(Arguments.ResultDirectoryArgumentName, "");

            SetSettingsFromArgs(args);

            if (string.IsNullOrWhiteSpace(ResultDirectory))
            {
                return;
            }

            var scenarios = GetScenarios(ResultDirectory);
            scenarios = AddTearDownOutputToScenarios(scenarios);
            
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

            PruneData();

            HtmlReportBuilder.CreateReport(dataToOutput);

            try
            {
                CopyFolder.Copy(Path.Combine(FileLocations.ProjectDirectory, "css"), Path.Combine(ResultDirectory, "css"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static List<Scenario> AddTearDownOutputToScenarios(List<Scenario> scenarios)
        {
            var scenariosFolder = Directory.GetDirectories(ResultDirectory, FileNames.Scenarios).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(scenariosFolder))
            {
                throw new ArgumentNullException(nameof(scenariosFolder), $"Can't find '{FileNames.Scenarios} folder in directory {ResultDirectory}");
            }

            var outputFiles = Directory.GetFiles(scenariosFolder).Where(name => name.Contains("TearDownOutput-"));

            var outputFilesByTestId = outputFiles.GroupBy(outputFile =>
                Path.GetFileNameWithoutExtension(outputFile)
                    .Split(new[] {"-TearDownOutput-"}, StringSplitOptions.None)[1]);
            
            foreach (var groupedOutputFiles in outputFilesByTestId)
            {
                var scenarioFrameworkTestId = groupedOutputFiles.Key;

                var foundScenario = scenarios.FirstOrDefault(scenario => scenario.FrameworkTestId == scenarioFrameworkTestId);

                if (foundScenario == null)
                {
                    continue;
                }

                foreach (var outputFile in groupedOutputFiles)
                {
                    foundScenario.TearDownOutput += File.ReadAllText(outputFile) + Environment.NewLine;   
                }
            }

            return scenarios;
        }

        private static void PruneData()
        {
            if (string.IsNullOrWhiteSpace(BDTestSettings.PersistentResultsDirectory))
            {
                return;
            }

            var filesTooOld = Directory.GetFiles(BDTestSettings.PersistentResultsDirectory).Where(filePath => File.GetCreationTime(filePath) < BDTestSettings.PrunePersistentDataOlderThan).ToList();
            foreach (var fileTooOld in filesTooOld)
            {
                File.Delete(fileTooOld);
            }

            var filesOverLimit = Directory.GetFiles(BDTestSettings.PersistentResultsDirectory).OrderBy(File.GetCreationTime).ToList();
            var count = filesOverLimit.Count();

            if (count <= BDTestSettings.PersistentFileCountToKeep)
            {
                return;
            }

            var amountToDelete = count - BDTestSettings.PersistentFileCountToKeep;
            foreach (var fileToPrune in filesOverLimit.Take(amountToDelete))
            {
                File.Delete(fileToPrune);
            }
        }

        private static void SetSettingsFromArgs(string[] args)
        {
            BDTestSettings.PersistentResultsDirectory = GetArgument(args, Arguments.PersistentStorageArgumentName);

            var persistentCompareStartDate = GetArgument(args, Arguments.PersistentResultsCompareStartTimeArgumentName);
            if (!string.IsNullOrWhiteSpace(persistentCompareStartDate))
            {
                BDTestSettings.PersistentResultsCompareStartTime = DateTime.ParseExact(persistentCompareStartDate, "o", CultureInfo.InvariantCulture);
            }

            BDTestSettings.AllScenariosReportHtmlFilename = GetArgument(args, Arguments.AllScenariosReportHtmlFilenameArgumentName);

            BDTestSettings.ScenariosByStoryReportHtmlFilename = GetArgument(args, Arguments.ScenariosByStoryReportHtmlFilenameArgumentName);

            BDTestSettings.FlakinessReportHtmlFilename = GetArgument(args, Arguments.FlakinessReportHtmlFilenameArgumentName);

            BDTestSettings.TestTimesReportHtmlFilename = GetArgument(args, Arguments.TestTimesReportHtmlFilenameArgumentName);

            BDTestSettings.XmlDataFilename = GetArgument(args, Arguments.XmlDataFilenameArgumentName);

            BDTestSettings.JsonDataFilename = GetArgument(args, Arguments.JsonDataFilenameArgumentName);

            if (string.IsNullOrWhiteSpace(BDTestSettings.PersistentResultsDirectory))
            {
                return;
            }

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
            try
            {
                JsonConvert.DeserializeXNode(jsonData, "TestData", true, true).WriteTo(new XmlTextWriter(Path.Combine(ResultDirectory, BDTestSettings.XmlDataFilename ?? FileNames.TestDataXml), Encoding.UTF8));
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(ResultDirectory, "BDTest - XML Write Exception.txt"), e.Message + Environment.NewLine + e.StackTrace);
            }
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

            if (string.IsNullOrWhiteSpace(scenariosFolder))
            {
                throw new ArgumentNullException(nameof(scenariosFolder), $"Can't find '{FileNames.Scenarios} folder in directory {resultDirectory}");
            }

            var scenarios = Directory.GetFiles(scenariosFolder).Where(name => !name.Contains("TearDownOutput-")).Select(scenarioFile =>
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
