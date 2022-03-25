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
        Assert.That(_scenario.Steps[0].Output, Is.EqualTo("run a test"));
        Assert.That(_scenario.Steps[1].Output, Is.EqualTo("the results should have set up and tear down information added"));
            
        Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].TestStartupInformation").ToString().Trim(), Is.EqualTo("Some startup info!"));
    }
    
    [Test]
    public void Test2()
    {
        _scenario = When(() => Console.WriteLine("run a test2"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added2"))
            .BDTest();
            
        Assert.That(_scenario.TestStartupInformation.Trim(), Is.EqualTo("Some startup info!"));
        Assert.That(_scenario.Steps[0].Output, Is.EqualTo("run a test2"));
        Assert.That(_scenario.Steps[1].Output, Is.EqualTo("the results should have set up and tear down information added2"));
    }
}