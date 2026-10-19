using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that custom scenario text attributes work properly",
    SoThat = "tests can be translated to plain English")]
public class ScenarioTextTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();


    [Test]
    [ScenarioText("My test that does stuff")]
    public async Task CustomScenarioText()
    {
        var scenario = await When(() => Console.WriteLine("my test has a scenario text"))
            .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
            .BDTestAsync();

        await Assert.That(scenario.GetScenarioText()).IsEqualTo("My test that does stuff");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.Scenarios[0].ScenarioText")).ToString()).IsEqualTo("My test that does stuff");
    }
        
    [Test]
    public async Task NoScenarioText()
    {
        var scenario = await When(() => Console.WriteLine("my test has no scenario text"))
            .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
            .BDTestAsync();
            
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("No scenario text");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.Scenarios[0].ScenarioText")).ToString()).IsEqualTo("No scenario text");
    }
        
    [Test]
    public async Task No_Scenario_Text_with_Hyphens()
    {
        var scenario = await When(() => Console.WriteLine("my test has no scenario text"))
            .Then(() => Console.WriteLine("the scenario text should be set to the method name"))
            .BDTestAsync();
            
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("No Scenario Text with Hyphens");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.Scenarios[0].ScenarioText")).ToString()).IsEqualTo("No Scenario Text with Hyphens");
    }

    [Test]
    [ScenarioText("N/A Text")]
    public async Task Attribute_ScenarioText_Is_Overwritten_When_WithScenarioText_MethodIsCalled()
    {
        var scenario = await When(() => Console.WriteLine("my test an attribute scenario text"))
            .Then(() => Console.WriteLine("the scenario text should be overwritten with the method value"))
            .WithScenarioText("Method ScenarioText overwrites attribute ScenarioText")
            .BDTestAsync();
            
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("Method ScenarioText overwrites attribute ScenarioText");
    }
        
    [Test]
    public async Task WithScenarioText_WithNoAttribute_SetsScenarioText()
    {
        var scenario = await When(() => Console.WriteLine("my test has no scenario text attribute"))
            .Then(() => Console.WriteLine("the scenario text should be taken from the method value"))
            .WithScenarioText("My Method ScenarioText")
            .BDTestAsync();
            
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("My Method ScenarioText");
    }
}
