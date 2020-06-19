using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Paths;
using BDTest.ReportGenerator.Builders;
using BDTest.ReportGenerator.Extensions;
using BDTest.ReportGenerator.Utils;
using BDTest.Settings;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator
{
    public static class BDTestReportGenerator
    {
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(Path.Combine(FileLocations.ReportsOutputDirectory, "BDTest - Report Exception.txt"), (e.ExceptionObject as Exception)?.StackTrace);
        }

        public static void GenerateInFolder(string folderPath)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            CreateReportFolder(folderPath);
            CreatePersistentResults(folderPath);

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            var scenarios = BDTestUtil.GetScenarios();
            
            var testTimer = GetTestTimer(scenarios);

            // Original File Names
            var reportPathByStory = Path.Combine(folderPath, FileNames.ReportByStory);
            var reportPathAllScenarios = Path.Combine(folderPath, FileNames.ReportAllScenarios);
            var testDataJsonPath = Path.Combine(folderPath, FileNames.TestDataJson);

            DeleteExistingFiles(reportPathByStory, reportPathAllScenarios, testDataJsonPath);

            var dataToOutput = new BDTestOutputModel
            {
                Id = BDTestUtil.GetInstanceGuid,
                Scenarios = scenarios,
                TestTimer = testTimer,
                NotRun = BDTestUtil.GetNotRunScenarios(),
                Version = BDTestVersionHelper.CurrentVersion
            };

            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            var jsonData = JsonConvert.SerializeObject(dataToOutput, Formatting.Indented, settings);
            
            WriteJsonOutput(folderPath, jsonData);

            PruneData();

            Directory.CreateDirectory(folderPath);

            HtmlReportBuilder.CreateReport(folderPath, dataToOutput);

            try
            {
                if (folderPath != FileLocations.ReportsOutputDirectory || FileLocations.RawOutputDirectory != FileLocations.ReportsOutputDirectory)
                {
                    CopyFolder.Copy(Path.Combine(FileLocations.RawOutputDirectory, "bdtest-reportdependencies"),
                        Path.Combine(folderPath, "bdtest-reportdependencies"));
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.Message);
            }
        }
        
        public static void Generate()
        {
            GenerateInFolder(FileLocations.ReportsOutputDirectory);
        }

        private static void CreateReportFolder(string folderPath)
        {
            // Relative file path - They've just given us a folder name probably. So let's use the current output directory.
            if (!Path.IsPathRooted(folderPath))
            {
                folderPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    folderPath
                );
            }
            
            if (Directory.Exists(folderPath) && folderPath != FileLocations.RawOutputDirectory)
            {
                var files = Directory.GetFiles(folderPath);

                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            
            Directory.CreateDirectory(folderPath);
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
            var count = filesOverLimit.Count;

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

        private static void CreatePersistentResults(string folderPath)
        {
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
                File.WriteAllText(Path.Combine(folderPath, "BDTest - Persistent Directory Error.txt"), e.StackTrace);
                BDTestSettings.PersistentResultsDirectory = null;
            }
        }

        private static void WriteJsonOutput(string folderPath, string jsonData)
        {
            try
            {
                File.WriteAllText(Path.Combine(folderPath, BDTestSettings.JsonDataFilename ?? FileNames.TestDataJson), jsonData);

                if (!string.IsNullOrWhiteSpace(BDTestSettings.PersistentResultsDirectory))
                {
                    File.Copy(Path.Combine(folderPath, BDTestSettings.JsonDataFilename ?? FileNames.TestDataJson),
                        Path.Combine(BDTestSettings.PersistentResultsDirectory, FileNames.TestDataJson));
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(folderPath, "BDTest - JSON Write Exception.txt"), e.Message + Environment.NewLine + e.StackTrace);
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

            return testTimer;
        }
    }
}
