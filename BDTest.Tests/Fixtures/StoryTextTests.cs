using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.None)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest works",
    SoThat = "other developers can use it confidently")]
public class StoryTextTests : BDTestBase
{
    [SetUp]
    public void Setup()
    {
        TestResetHelper.ResetData();
    }

    [Test]
    public void CustomStoryText()
    {
        var scenario = When(() => Console.WriteLine("my test has a story text"))
            .Then(() => Console.WriteLine("the attribute should be serialized to the json output"))
            .BDTest();
            
        Assert.That(scenario.GetStoryText(), Is.EqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}"));
        Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].StoryText").ToString(), Is.EqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}"));
    }

    [Test]
    public void GetStoryTextFromBaseClassObject()
    {
        Assert.That(GetStoryText(), Is.EqualTo($"As a BDTest developer{Environment.NewLine}I want to make sure that BDTest works{Environment.NewLine}So that other developers can use it confidently{Environment.NewLine}"));
    }
}

[Parallelizable(ParallelScope.None)]
public class NoStoryTextTests : BDTestBase
{
    [SetUp]
    public void Setup()
    {
        TestResetHelper.ResetData();
    }
        
    [Test]
    public void NoStoryText()
    {
        var scenario = When(() => Console.WriteLine("my test has no story text"))
            .Then(() => Console.WriteLine("the scenario text should be null"))
            .BDTest();
            
        Assert.That(scenario.GetStoryText(), Is.EqualTo(DefaultValues.StoryTextNotDefined));
        Assert.That(JsonHelper.GetTestDynamicJsonObject().SelectToken("$.Scenarios[0].StoryText").Type, Is.EqualTo(JTokenType.Null));
    }
}