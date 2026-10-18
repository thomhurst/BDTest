using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.None)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that specific exceptions can be registered as 'passable'",
    SoThat = "BDTest works well with other frameworks such as NUnit")]
public class CustomPassExceptionsTests : NUnitBDTestBase<MyTestContext>
{
    [Test]
    public void NUNitSuccessException()
    {
        var builder = Given(() => Console.WriteLine("NUnit throws a success exception"))
            .When(() => Assert.Pass())
            .Then(() => Console.WriteLine("the test has passed"));

        try
        {
            builder.BDTest();

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (SuccessException)
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }
    }

    [Test]
    public void CustomException()
    {
        using var registration = new ExceptionRegistration(
            BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes, typeof(MyAllowedException));

        var builder = Given(() => Console.WriteLine("NUnit throws a success exception"))
            .When(() => ThrowException())
            .Then(() => Console.WriteLine("the test has passed"));

        try
        {
            builder.BDTest();

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (MyAllowedException)
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }
    }

    private void ThrowException()
    {
        throw new MyAllowedException();
    }
}

public class MyAllowedException : Exception
{
}