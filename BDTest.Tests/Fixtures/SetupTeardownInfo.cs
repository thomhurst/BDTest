using BDTest.NUnit;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class SetupTeardownInfo : NUnitBDTestBase<MyTestContext>
{
    private Scenario? _scenario;

    [SetUp]
    public void WriteStartupInformation()
    {
        TestResetHelper.ResetData();
        Console.Out.WriteLine("Some startup info!");
    }
        
    [TearDown]
    public void TearDown()
    {
        Console.Out.WriteLine("Some teardown info!");
            
        Assert.That(Required(_scenario).TearDownOutput.Trim(), Is.EqualTo("Some teardown info!"));
            
        Assert.That(Required(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].TearDownOutput")).ToString().Trim(), Is.EqualTo("Some teardown info!"));
    }
        
    [Test]
    public async Task Test()
    {
        _scenario = await When(() => Console.WriteLine("run a test"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added"))
            .BDTestAsync();
            
        Assert.That(_scenario.TestStartupInformation.Trim(), Is.EqualTo("Some startup info!"));
        Assert.That(_scenario.Steps[0].Output, Is.EqualTo("run a test"));
        Assert.That(_scenario.Steps[1].Output, Is.EqualTo("the results should have set up and tear down information added"));
            
        Assert.That(Required(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].TestStartupInformation")).ToString().Trim(), Is.EqualTo("Some startup info!"));
    }
    
    [Test]
    public async Task Test2()
    {
        _scenario = await When(() => Console.WriteLine("run a test2"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added2"))
            .BDTestAsync();
            
        Assert.That(_scenario.TestStartupInformation.Trim(), Is.EqualTo("Some startup info!"));
        Assert.That(_scenario.Steps[0].Output, Is.EqualTo("run a test2"));
        Assert.That(_scenario.Steps[1].Output, Is.EqualTo("the results should have set up and tear down information added2"));
    }
}