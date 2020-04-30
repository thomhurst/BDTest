using System;
using System.IO;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests
{
    [Parallelizable(ParallelScope.None)]
    public class ScenarioTextTests : BDTestBase
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
        [ScenarioText("My test that does stuff")]
        public void CustomScenarioText()
        {
            var scenario = When(() => Console.WriteLine("my test has a scenario text"))
                .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("My test that does stuff"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText.Scenario").ToString(), Is.EqualTo("My test that does stuff"));
        }
        
        [Test]
        public void NoScenarioText()
        {
            var scenario = When(() => Console.WriteLine("my test has no scenario text"))
                .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("No scenario text"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText.Scenario").ToString(), Is.EqualTo("No scenario text"));
        }
        
        [Test]
        public void No_Scenario_Text_with_Hyphens()
        {
            var scenario = When(() => Console.WriteLine("my test has no scenario text"))
                .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("No Scenario Text with Hyphens"));
            Assert.That(JsonHelper.GetDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText.Scenario").ToString(), Is.EqualTo("No Scenario Text with Hyphens"));
        }
    }
}