using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that I can use TestInformation attributes",
    SoThat = "my report has detailed information")]
public class TestInformationAttributeTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

        
    [Test]
    public async Task NoTestInformationAttribute()
    {
        var scenario = await When(() => Console.WriteLine("my test has no test information attribute"))
            .Then(() => Console.WriteLine("the information array should be empty"))
            .BDTestAsync();
            
        await Assert.That(scenario.CustomTestInformation).IsEmpty();
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].customTestInformation")).ToString()).IsEqualTo("[]");
    }

    [Test]
    [TestInformation("Testing 1 Information Attribute")]
    public async Task TestInformationAttribute1()
    {
        var scenario = await When(() => Console.WriteLine("my test has an attribute"))
            .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
            .BDTestAsync();
            
        await Assert.That(Required(scenario.CustomTestInformation).First()).IsEqualTo($"Testing 1 Information Attribute");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].customTestInformation[0]")).ToString()).IsEqualTo("Testing 1 Information Attribute");
    }
        
    [Test]
    [TestInformation("Testing 1 Information Attribute")]
    [CustomInformation("Testing 2 Information Attributes")]
    public async Task TestInformationAttribute2()
    {
        var scenario = await When(() => Console.WriteLine("my test has multiple attributes"))
            .Then(() => Console.WriteLine("the attributes should be serialized to the json output"))
            .BDTestAsync();
            
        await Assert.That(Required(scenario.CustomTestInformation).First()).IsEqualTo($"Testing 1 Information Attribute");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].customTestInformation[0]")).ToString()).IsEqualTo("Testing 1 Information Attribute");
        await Assert.That(scenario.CustomTestInformation[1]).IsEqualTo($"Testing 2 Information Attributes");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].customTestInformation[1]")).ToString()).IsEqualTo("Testing 2 Information Attributes");
    }
}
