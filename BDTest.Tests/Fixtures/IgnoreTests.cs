using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that specific exceptions can be marked as 'skippable'",
    SoThat = "BDTest works well with other frameworks such as NUnit")]
[Parallelizable(ParallelScope.None)]
public class IgnoreTests : LegacyNUnitBDTestBase<MyTestContext>
{
    [Test]
    public async Task NUNitIgnoreException()
    {
        var builder = Given(() => Console.WriteLine("NUnit throws an ignore exception"))
            .When(() => Assert.Ignore())
            .Then(() => Console.WriteLine("the test is ignored"));

        try
        {
            await builder.BDTestAsync();
                
            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch(IgnoreException)
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
                
            Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        }
    }
        
    [Test]
    public async Task CustomIgnoreException()
    {
        using var registration = new ExceptionRegistration(
            BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes, typeof(MyCustomIgnoreException));
        var builder = Given(() => Console.WriteLine("NUnit throws an ignore exception"))
            .When(() => ThrowException())
            .Then(() => Console.WriteLine("the test is ignored"));

        try
        {
            await builder.BDTestAsync();
                
            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch(MyCustomIgnoreException)
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
                
            Assert.That(scenario.Status, Is.EqualTo(Status.Inconclusive));
        }
    }

    private void ThrowException()
    {
        throw new MyCustomIgnoreException();
    }
}

public class MyCustomIgnoreException : Exception
{
}