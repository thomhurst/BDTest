using System;
using System.Collections.Generic;
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
    internal class ReportProgram
    {
        public static string ResultDirectory;
        public static string PersistentStorage;

        public static void Main(string[] args)
        {

            ResultDirectory = args.FirstOrDefault(it => it.StartsWith(WriteOutput.ResultDirectoryArgumentName))?.Replace(WriteOutput.ResultDirectoryArgumentName, "");
            PersistentStorage = args.FirstOrDefault(it => it.StartsWith(WriteOutput.PersistentStorageArgumentName))?.Replace(WriteOutput.PersistentStorageArgumentName, "");

            if (!string.IsNullOrWhiteSpace(PersistentStorage))
            {
                try
                {
                    Directory.CreateDirectory(PersistentStorage);
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(ResultDirectory, "BDTest - Exception.txt"), e.StackTrace);
                    PersistentStorage = null;
                }
            }

            Console.WriteLine($"Results Directory is: {ResultDirectory}");

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
           catch (Exception e)
           {
               Console.WriteLine(e);
           }
        }

        private static void WriteJsonOutput(string jsonData)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, FileNames.TestDataJson), jsonData);

            if (!string.IsNullOrWhiteSpace(PersistentStorage))
            {
                File.Copy(Path.Combine(ResultDirectory, FileNames.TestDataJson),
                    Path.Combine(PersistentStorage, FileNames.TestDataJson));
            }
        }

        private static void WriteXmlOutput(string jsonData)
        {
            File.WriteAllText(Path.Combine(ResultDirectory, FileNames.TestDataXml),
                JsonConvert.DeserializeXmlNode(jsonData, "TestData").ToXmlString());

//            if (!string.IsNullOrWhiteSpace(PersistentStorage))
//            {
//                File.Copy(Path.Combine(ResultDirectory, FileNames.TestDataXml),
//                    Path.Combine(PersistentStorage, FileNames.TestDataXml));
//            }
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
            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static List<Scenario> GetScenarios(string resultDirectory)
        {
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                Error = (se, ev) => { ev.ErrorContext.Handled = true; }
            };
            var scenariosFolder = Directory.GetDirectories(resultDirectory, FileNames.Scenarios).FirstOrDefault() ?? throw new Exception($"Can't find '{FileNames.Scenarios} folder in directory {resultDirectory}");

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

            if(enumerable.Count == 0) return new TestTimer();

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
