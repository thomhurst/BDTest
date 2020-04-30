using System;
using System.IO;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests
{
    [Parallelizable(ParallelScope.None)]
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that BDTest works",
        SoThat = "other developers can use it confidently")]
    public class StoryTextTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            BDTestSettings.ReportFolderName = "CustomFolder";
            
            if (FileHelpers.HasCustomFolder() && File.Exists(FileHelpers.GetOutputFolder()))
            {
                Directory.Delete(FileHelpers.GetOutputFolder(), true);
            }
        }

        [Test]
        public void CustomStoryText()
        {
            var scenario = When(() => Console.WriteLine("my test has a story text"))
                .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.GetStoryText(), Is.EqualTo("As a BDTest developer\r\nI want to make sure that BDTest works\r\nSo that other developers can use it confidently\r\n"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].StoryText.Story").ToString(), Is.EqualTo("As a BDTest developer\r\nI want to make sure that BDTest works\r\nSo that other developers can use it confidently\r\n"));
        }
    }

    [Parallelizable(ParallelScope.None)]
    public class NoStoryTextTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            BDTestSettings.ReportFolderName = "CustomFolder";
            
            if (FileHelpers.HasCustomFolder() && File.Exists(FileHelpers.GetOutputFolder()))
            {
                Directory.Delete(FileHelpers.GetOutputFolder(), true);
            }
        }
        
        [Test]
        public void NoStoryText()
        {
            var scenario = When(() => Console.WriteLine("my test has no story text"))
                .Then(() => Console.WriteLine("the scenario text should be null"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.GetStoryText(), Is.EqualTo("Story Text Not Defined"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].StoryText").Type, Is.EqualTo(JTokenType.Null));
        }
    }
}