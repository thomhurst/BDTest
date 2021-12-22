using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using QuestPDF.Fluent;

namespace BDTest.ReportGenerator.PDF
{
    public class BDTestPdfGenerator
    {
        public static string GeneratePdfFile(BDTestRunDescriptor bdTestRunDescriptor)
        {
            var scenarios = BDTestUtil.GetScenarios();

            var testTimer = GetTestTimer(scenarios);

            var dataToOutput = new BDTestOutputModel
            {
                Id = BDTestUtil.GetCurrentReportId,
                Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.Environment,
                Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.Tag,
                BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.BranchName,
                MachineName = Environment.MachineName,
                Scenarios = scenarios,
                TestTimer = testTimer,
                NotRun = BDTestUtil.GetNotRunScenarios(),
                Version = BDTestVersionHelper.CurrentVersion
            };

            var filePath = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".pdf"; 
            
            new ReportDocument(dataToOutput).GeneratePdf(filePath);
            
            Console.WriteLine($"File Path: {filePath}");

            return filePath;
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