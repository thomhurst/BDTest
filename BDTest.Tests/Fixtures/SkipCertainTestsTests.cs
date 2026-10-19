using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that certain tests can be skipped conditionally",
    SoThat = "tests can be run with different behaviour for different test environments")]
public class SkipCertainTestsTests : RunTestBase
{
    [SetUp]
    public void Setup()
    {
        Options.SkipTestRules.Add(test => test.BdTestBaseClass?.GetType() == typeof(SkipCertainTestsTests));
    }

    [Test]
    public async Task Skip1()
    {
        var test = await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowException1())
            .BDTestAsync();
            
        Assert.That(test.Status, Is.EqualTo(Status.Skipped));
    }
        
    [Test]
    public async Task Skip2()
    {
        var test = await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowException1())
            .And(() => ThrowException1())
            .And(() => ThrowException1())
            .BDTestAsync();
            
        Assert.That(test.Status, Is.EqualTo(Status.Skipped));
    }

    [Test]
    public async Task Skip3()
    {
        var test = await Given(() => Console.WriteLine("A test"))
            .When(() => Console.WriteLine())
            .Then(() => Console.WriteLine("The test passes because the exception step was skipped"))
            .And(() => ThrowException1())
            .BDTestAsync();
            
        Assert.That(test.Status, Is.EqualTo(Status.Skipped));
    }

    private void ThrowException1()
    {
        throw new Exception();
    }
}
