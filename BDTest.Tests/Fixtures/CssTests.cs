using System;
using System.IO;
using System.Linq;
using BDTest.ReportGenerator;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
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

            Assert.That(foldersInDirectory, Contains.Item("css"));

            var filesInCssFolder = Directory.GetFiles(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "css")).Select(Path.GetFileName).ToList();

            Assert.That(filesInCssFolder, Contains.Item("testy.css"));
            Assert.That(filesInCssFolder, Contains.Item("jquery-3.3.1.min.js"));
            Assert.That(filesInCssFolder, Contains.Item("checkbox_toggle_js.js"));
            
            var foldersInCssFolder = Directory.GetDirectories(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "css")).Select(Path.GetFileName).ToList();
            
            Assert.That(foldersInCssFolder, Contains.Item("milligram"));
            
            var filesInMilligramFolder = Directory.GetFiles(Path.Combine(FileHelpers.GetUniqueTestOutputFolder(), "css", "milligram", "dist")).Select(Path.GetFileName).ToList();
            
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.css"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.css.map"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.min.css"));
            Assert.That(filesInMilligramFolder, Contains.Item("milligram.min.css.map"));
        }
    }
}