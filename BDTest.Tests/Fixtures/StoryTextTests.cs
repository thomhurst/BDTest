using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works",
    SoThat = "other developers can use it confidently")]
public class StoryTextTests : RunTestBase
{

    [Test]
    public async Task CustomStoryText()
    {
        var scenario = await When(() => Console.WriteLine("my test has a story text"))
            .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
            .BDTestAsync();
            
        await Assert.That(scenario.StoryText).IsEqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}");
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].storyText")).ToString()).IsEqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}");
    }

    [Test]
    public async Task GetStoryTextFromBaseClassObject()
    {
        await Assert.That(GetStoryText()).IsEqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}");
    }
}

[NotInParallel]
public class NoStoryTextTests : RunTestBase
{
        
    [Test]
    public async Task NoStoryText()
    {
        var scenario = await When(() => Console.WriteLine("my test has no story text"))
            .Then(() => Console.WriteLine("the scenario text should be null"))
            .BDTestAsync();
            
        await Assert.That(scenario.StoryText).IsEqualTo(DefaultValues.StoryTextNotDefined);
        await Assert.That(Required(JsonHelper.GetTestDynamicJsonObject(TestRun).SelectToken("$.scenarios[0].storyText")).ToString()).IsEqualTo(scenario.StoryText);
    }
}
