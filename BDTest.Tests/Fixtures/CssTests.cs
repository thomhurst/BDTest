using System;
using System.IO;
using System.Linq;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that css is copied to the output directory",
        SoThat = "locally generated reports are styled correctly")]
    public class CssTests : BDTestBase
    {
        [Test]
        public void EnsureCssExists()
        {
            When(() => Console.WriteLine("A report is generated"))
                .Then(() => Console.WriteLine("the CSS folder is copied to the report folder"))
                .BDTest();
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());

            var foldersInDirectory = Directory.GetDirectories(FileHelpers.GetUniqueTestOutputFolder()).Select(Path.GetFileName).ToList();

            Assert.That(foldersInDirectory, Contains.Item("bdtest-reportdependencies"));

            var filesInCssFolder = Directory.GetFiles(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "bdtest-reportdependencies")).Select(Path.GetFileName).ToList();

            Assert.That(filesInCssFolder, Contains.Item("bdtest.css"));
            Assert.That(filesInCssFolder, Contains.Item("jquery-3.3.1.min.js"));
            Assert.That(filesInCssFolder, Contains.Item("checkbox_toggle_js.js"));
            
            var foldersInCssFolder = Directory.GetDirectories(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "bdtest-reportdependencies")).Select(Path.GetFileName).ToList();
            
            Assert.That(foldersInCssFolder, Contains.Item("milligram"));
            
            var filesInMilligramFolder = Directory.GetFiles(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "bdtest-reportdependencies", "milligram", "dist")).Select(Path.GetFileName).ToList();
            
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.css"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.css.map"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.min.css"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.min.css.map"));
        }
    }
}