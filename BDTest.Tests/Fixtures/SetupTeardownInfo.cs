using BDTest.NUnit;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class SetupTeardownInfo : NUnitBDTestBase<MyTestContext>
{
    private Scenario _scenario;

    [SetUp]
    public void Setup()
    {
        TestResetHelper.ResetData();
        Console.Out.WriteLine("Some startup info!");
    }
        
    [TearDown]
    public void TearDown()
    {
        Console.Out.WriteLine("Some teardown info!");
            
        Assert.That(_scenario.TearDownOutput.Trim(), Is.EqualTo("Some teardown info!"));
            
        Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].TearDownOutput").ToString().Trim(), Is.EqualTo("Some teardown info!"));
    }
        
    [Test]
    public void Test()
    {
        _scenario = When(() => Console.WriteLine("run a test"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added"))
            .BDTest();
            
        Assert.That(_scenario.TestStartupInformation.Trim(), Is.EqualTo("Some startup info!"));
            
        Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].TestStartupInformation").ToString().Trim(), Is.EqualTo("Some startup info!"));
    }
}