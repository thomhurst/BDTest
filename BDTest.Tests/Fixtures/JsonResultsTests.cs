using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.NUnit;
using BDTest.Test;
using BDTest.Tests.Helpers;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works",
    SoThat = "other developers can use it confidently")]
[Parallelizable(ParallelScope.None)]
public class JsonResultsTests : NUnitBDTestBase<MyTestContext>
{
    [SetUp, TearDown]
    public void Setup()
    { 
        TestResetHelper.ResetData();    
    }
        
    [Test]
    [TestInformation("Some info!")]
    [ScenarioText("Can Deserialize JSON")]
    public void CanDeserializeJsonResultsSuccessfully()
    {
        When(() => Console.WriteLine("A persistent json file is written")).WithStepText(() => "I write custom when step text")
            .Then(() => CustomStep("1", "2"))
            .BDTest();

        var inMemoryScenario = BDTestUtil.GetScenarios().Single();
            
        var json = BDTestJsonHelper.GetTestJsonData();
        var jObject = JObject.Load(new JsonTextReader(new StringReader(json)));

        var scenarios = JsonConvert.DeserializeObject<List<Scenario>>(jObject.GetValue("Scenarios").ToString());
            
        Assert.That(scenarios, Is.Not.Null);
        Assert.That(scenarios.Count, Is.EqualTo(1));

        var deserializedScenario = scenarios.First();
            
        Assert.That(deserializedScenario.Steps[0].StepText, Is.EqualTo("When I write custom when step text"));
        Assert.That(deserializedScenario.Steps[1].StepText, Is.EqualTo("Then 1 2"));

        var compareLogic = new CompareLogic
        {
            Config = new ComparisonConfig
            {
                MaxDifferences = int.MaxValue,
                // The below are runtime only, and so we don't serialize.
                MembersToIgnore =
                {
                    nameof(Scenario.BdTestBaseClass)
                }
            }
        };

        var comparisonResult = compareLogic.Compare(inMemoryScenario, deserializedScenario);
            
        Assert.That(comparisonResult.AreEqual, Is.True);
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