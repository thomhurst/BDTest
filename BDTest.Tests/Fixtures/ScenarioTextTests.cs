using System;
using BDTest.Attributes;
using BDTest.ReportGenerator;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Parallelizable(ParallelScope.None)]
    public class ScenarioTextTests : BDTestBase
    {
        [SetUp]
        public void Setup()
        {
            TestSetupHelper.ResetData();
        }

        [Test]
        [ScenarioText("My test that does stuff")]
        public void CustomScenarioText()
        {
            var scenario = When(() => Console.WriteLine("my test has a scenario text"))
                .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
                .BDTest();
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("My test that does stuff"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText").ToString(), Is.EqualTo("My test that does stuff"));
        }
        
        [Test]
        public void NoScenarioText()
        {
            var scenario = When(() => Console.WriteLine("my test has no scenario text"))
                .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
                .BDTest();
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("No scenario text"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText").ToString(), Is.EqualTo("No scenario text"));
        }
        
        [Test]
        public void No_Scenario_Text_with_Hyphens()
        {
            var scenario = When(() => Console.WriteLine("my test has no scenario text"))
                .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
                .BDTest();
            
            BDTestReportGenerator.GenerateInFolder(FileHelpers.GetUniqueTestOutputFolder());

            Assert.That(scenario.GetScenarioText(), Is.EqualTo("No Scenario Text with Hyphens"));
            Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].ScenarioText").ToString(), Is.EqualTo("No Scenario Text with Hyphens"));
        }
    }
}