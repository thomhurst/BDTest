using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.ReportGenerator;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [Story(AsA = "BDTest developer",
        IWant = "to make sure that BDTest works",
        SoThat = "other developers can use it confidently")]
    [Parallelizable(ParallelScope.None)]
    public class PersistentResultsTests : NUnitBDTestBase<MyTestContext>
    {
        [SetUp]
        public void Setup()
        { 
            TestSetupHelper.ResetData();    
        }
        
        [Test]
        [TestInformation("Some info!")]
        [ScenarioText("Can Deserialize Persistent JSON File")]
        public void CanDeserializePersistentTestResultsSuccessfully()
        {
            BDTestSettings.ReportSettings.PersistentResultsDirectory =
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Persistent");

            When(() => Console.WriteLine("A persistent json file is written")).WithStepText(() => "I write custom when step text")
                .Then(() => CustomStep("1", "2"))
                .BDTest();
            
            BDTestReportGenerator.Generate();

            var persistentJson = Directory.GetFiles(BDTestSettings.ReportSettings.PersistentResultsDirectory).First();
            var jObject = JObject.Load(new JsonTextReader(new StringReader(File.ReadAllText(persistentJson))));

            var scenarios = JsonConvert.DeserializeObject<List<Scenario>>(jObject.GetValue("Scenarios").ToString());
            
            Assert.That(scenarios, Is.Not.Null);
            Assert.That(scenarios.Count, Is.GreaterThanOrEqualTo(1));
            
            Assert.That(scenarios.First().Steps[0].StepText, Is.EqualTo("When I write custom when step text"));
            Assert.That(scenarios.First().Steps[1].StepText, Is.EqualTo("Then 1 2"));
        }
        
        [StepText("{0} {1}")]
        private static void CustomStep(string one, string two)
        {
            var result = int.Parse(one) + int.Parse(two);
            Console.WriteLine($"Parameters are {one} {two}");
            Console.WriteLine($"Result is {result}");
            Console.WriteLine("then it can also be deserialized successfully");
        }
    }
}