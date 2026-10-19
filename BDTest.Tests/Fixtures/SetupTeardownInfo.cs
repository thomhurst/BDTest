using BDTest.TUnit;
using BDTest.Helpers;
using Newtonsoft.Json.Linq;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class SetupTeardownInfo : TUnitBDTestBase<MyTestContext>
{
    private Scenario? _scenario;

    private JToken ScenarioJson() => Required(JObject.Parse(
        BDTestJsonHelper.GetTestJsonData(null, TestRun))["Scenarios"])
        .Single(scenario => (string?)scenario["Guid"] == Required(_scenario).Guid);

    [Before(HookType.Test)]
    public void WriteStartupInformation()
    {
        Console.Out.WriteLine("Some startup info!");
    }
        
    [After(HookType.Test)]
    public async Task VerifyTeardownOutput()
    {
        Console.Out.WriteLine("Some teardown info!");
            
        await Assert.That(Required(Required(_scenario).TearDownOutput).Trim()).IsEqualTo("Some teardown info!");

        await Assert.That(Required(ScenarioJson()["TearDownOutput"]).ToString().Trim()).IsEqualTo("Some teardown info!");
    }
        
    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    public async Task Test()
    {
        _scenario = await When(() => Console.WriteLine("run a test"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added"))
            .BDTestAsync();
            
        await Assert.That(Required(_scenario.TestStartupInformation).Trim()).IsEqualTo("Some startup info!");
        await Assert.That(Required(_scenario.Steps)[0].Output).IsEqualTo("run a test");
        await Assert.That(_scenario.Steps[1].Output).IsEqualTo("the results should have set up and tear down information added");

        await Assert.That(Required(ScenarioJson()["TestStartupInformation"]).ToString().Trim()).IsEqualTo("Some startup info!");
    }
    
    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    public async Task Test2()
    {
        _scenario = await When(() => Console.WriteLine("run a test2"))
            .Then(() => Console.WriteLine("the results should have set up and tear down information added2"))
            .BDTestAsync();
            
        await Assert.That(Required(_scenario.TestStartupInformation).Trim()).IsEqualTo("Some startup info!");
        await Assert.That(Required(_scenario.Steps)[0].Output).IsEqualTo("run a test2");
        await Assert.That(_scenario.Steps[1].Output).IsEqualTo("the results should have set up and tear down information added2");
    }
}
