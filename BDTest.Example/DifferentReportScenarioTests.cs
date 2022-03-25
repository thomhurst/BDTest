using System;
using System.Threading.Tasks;
using BDTest.Attributes;
using NUnit.Framework;

namespace BDTest.Example;

[Story(AsA = "BDTest Developer",
    IWant = "to show how different states show in a test report",
    SoThat = "users can see what a report would look like")]
public class DifferentReportScenarioTests : MyTestBase
{
    [SetUp]
    public void Setup()
    {
        WriteStartupOutput("This is some startup custom text");
    }

    [TearDown]
    public void TearDown()
    {
        WriteTearDownOutput("This is some custom teardown text");
    }

    [Test]
    [ScenarioText("An ignored test")]
    public async Task TestIgnored()
    {
        await When(() => Ignore())
            .Then(() => Pass())
            .BDTestAsync();
    }
        
    [Test]
    [ScenarioText("An inconclusive given test")]
    public async Task TestGivenInconclusive()
    {
        await Given(() => Inconclusive()) 
            .When(() => Pass())
            .Then(() => Pass())
            .BDTestAsync();
    }
        
    [Test]
    [ScenarioText("An inconclusive when test")]
    public async Task TestWhenInconclusive()
    {
        await When(() => Inconclusive())
            .Then(() => Pass())
            .BDTestAsync();
    }
        
    [Test]
    [ScenarioText("An inconclusive then test")]
    public async Task TestThenInconclusive()
    {
        await When(() => Pass())
            .Then(() => Inconclusive())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("The given step failed")]
    public async Task GivenFailed()
    {
        await Given(() => Fail()) 
            .When(() => Pass())
            .Then(() => Pass())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("The and given step failed")]
    public async Task AndGivenFailed()
    {
        await Given(() => Pass())
            .And(() => Fail())
            .When(() => Pass())
            .Then(() => Pass())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("The when step failed")]
    public async Task WhenFailed()
    {
        await When(() => Fail())
            .Then(() => Pass())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("The then step failed")]
    public async Task ThenFailed()
    {
        await When(() => Pass())
            .Then(() => Fail())
            .BDTestAsync();
    }

    [Test]
    [ScenarioText("The and then step failed")]
    public async Task AndThenFailed()
    {
        await When(() => Pass())
            .Then(() => Pass())
            .And(() => Fail())
            .BDTestAsync();
    }
        
    [Test]
    [ScenarioText("A test that passes")]
    public async Task EverythingPasses()
    {
        await Given(() => Pass())
            .And(() => Pass())
            .When(() => Pass())
            .Then(() => Pass())
            .And(() => Pass())
            .BDTestAsync();
    }
        
    [Test]
    [ScenarioText("A custom link to attach to the report server")]
    public async Task CustomHtml()
    {
        await Given(() => Pass())
            .And(() => Pass())
            .When(() => Pass())
            .Then(() => Pass())
            .And(() => Pass())
            .BDTestAsync();
            
        ScenarioHtmlWriter.Link("BDTest Wiki!", "https://github.com/thomhurst/BDTest");
    }

    [StepText("the step passes")]
    private void Pass()
    {
        Assert.That(1, Is.EqualTo(1));
    }

    [StepText("I fail the step")]
    private void Fail()
    {
        throw new ArithmeticException();
    }

    private void Ignore()
    {
        Assert.Ignore();
    }

    private void Inconclusive()
    {
        Assert.Inconclusive();
    }
}