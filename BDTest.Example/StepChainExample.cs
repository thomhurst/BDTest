using System.Net;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example;

[Story(AsA = "BDTest user",
    IWant = "to use simple Step chaining without Given/When/Then",
    SoThat = "I can create clean step-based tests")]
public class StepChainExample : MyTestBase
{
    [Test]
    [ScenarioText("A test using simple Step chaining pattern")]
    public async Task TestWithStepChaining()
    {
        await Step(() => Steps.CreateAnAccount())
            .Step(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("A multi-step test using Step chaining")]
    public async Task TestWithMultipleSteps()
    {
        await Step(() => Steps.CreateAnAccount())
            .Step(() => Steps.NavigateToHomePage())
            .Step(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("A test comparing traditional BDD vs Step chaining")]
    public async Task TraditionalBDDTest()
    {
        await Given(() => Steps.CreateAnAccount())
            .When(() => Steps.NavigateToHomePage())
            .Then(() => Assertions.TheHttpStatusCodeIs(HttpStatusCode.NotFound))
            .BDTestAsync();
    }
}