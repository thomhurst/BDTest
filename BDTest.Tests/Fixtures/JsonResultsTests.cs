using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.TUnit;
using BDTest.Test;
using BDTest.Tests.Helpers;
using BDTest.TestUtilities;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works",
    SoThat = "other developers can use it confidently")]
[NotInParallel]
public class JsonResultsTests : RunTUnitTestBase<MyTestContext>
{
    [Before(HookType.Test), After(HookType.Test)]
    public void ResetScenarioData()
    { 

    }
        
    [Test]
    [TestInformation("Some info!")]
    [ScenarioText("Can Deserialize JSON")]
    public async Task CanDeserializeJsonResultsSuccessfully()
    {
        await When(() => Console.WriteLine("A persistent json file is written")).WithStepText(() => "I write custom when step text")
            .Then(() => CustomStep("1", "2"))
            .BDTestAsync();

        var inMemoryScenario = BDTestUtil.GetScenarios(TestRun).Single();
            
        var json = BDTestJsonHelper.GetTestJsonData(null, TestRun);
        var jObject = JObject.Load(new JsonTextReader(new StringReader(json)));

        var scenarios = BDTestJsonHelper.DeserializeReport(json).Scenarios;
            
        await Assert.That(scenarios).IsNotNull();
        await Assert.That(scenarios.Count).IsEqualTo(1);

        var deserializedScenario = scenarios.First();
            
        await Assert.That(Required(deserializedScenario.Steps)[0].StepText).IsEqualTo("When I write custom when step text");
        await Assert.That(deserializedScenario.Steps[1].StepText).IsEqualTo("Then 1 2");

        var compareLogic = new CompareLogic
        {
            Config = new ComparisonConfig
            {
                MaxDifferences = int.MaxValue,
                // The below are runtime only, and so we don't serialize.
                MembersToIgnore =
                {
                    nameof(Scenario.BdTestBaseClass),
                    nameof(Scenario.Run)
                }
            }
        };

        var comparisonResult = compareLogic.Compare(inMemoryScenario, deserializedScenario);
            
        await Assert.That(comparisonResult.AreEqual).IsTrue().Because(comparisonResult.DifferencesString);
    }
        
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HistoricalReportSurvivesCurrentCoreSerialization(bool writeIndented)
    {
        var report = ReportJson.DeserializeObject<BDTestOutputModel>(HistoricalReportFixture.ReadJson());
        HistoricalReportFixture.AssertCompatible(report);

        Func<BDTestOutputModel?, string> serializeCompact = BDTestJsonHelper.Serialize;
        var compact = serializeCompact(report);
        var json = BDTestJsonHelper.Serialize(report, writeIndented: writeIndented);
        await Assert.That(compact).IsEqualTo(BDTestJsonHelper.Serialize(report, writeIndented: false));
        await Assert.That(compact.Contains('\n')).IsFalse();
        await Assert.That(json.Contains('\n')).IsEqualTo(writeIndented);
        await Assert.That(serializeCompact(null)).IsEqualTo("null");
        var restored = ReportJson.DeserializeObject<BDTestOutputModel>(json);
        HistoricalReportFixture.AssertCompatible(restored);
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
