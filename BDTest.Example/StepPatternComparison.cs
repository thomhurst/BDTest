using System.Net;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example;

[Story(AsA = "BDTest user",
    IWant = "to use either Given/When/Then or simple Step chaining",
    SoThat = "I can choose the most appropriate pattern for my tests")]
public class StepPatternComparison : MyTestBase
{
    [Test]
    [ScenarioText("Traditional BDD test using Given/When/Then pattern")]
    public async Task TraditionalBDDPattern()
    {
        await Given(() => Steps.CreateAnAccount())
            .When(() => Steps.NavigateToHomePage())
            .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Simple test using Step chaining pattern")]
    public async Task StepChainingPattern()
    {
        await Step(() => Steps.CreateAnAccount())
            .Step(() => Steps.NavigateToHomePage())
            .Step(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Complex workflow using Step chaining")]
    public async Task ComplexWorkflowWithSteps()
    {
        await Step(() => Steps.CreateAnAccount())
            .Step(() => Steps.NavigateToHomePage())
            .Step(() => Console.WriteLine("Additional processing..."))
            .Step(() => Steps.CreateAnAccount()) // Simulating another action
            .Step(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .WithStepText(() => "Verify the final response status")
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Single step test")]
    public async Task SingleStepTest()
    {
        await Step(() => Steps.CreateAnAccount())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("Step pattern with custom step text")]
    public async Task StepWithCustomText()
    {
        await Step(() => Steps.CreateAnAccount())
            .WithStepText(() => "Setting up a new user account")
            .Step(() => Steps.NavigateToHomePage())
            .WithStepText(() => "Navigating to the main page")
            .BDTestAsync();
    }
}