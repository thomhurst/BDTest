using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.All)]
[BDTestRetry(3)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that tests can be retried",
    SoThat = "developers can mitigate against flakey tests")]
public class RetryTests : NUnitBDTestBase<RetryContext>
{
    private int _retryMethodCallCount;
        
    private int _setUpCounter = 0;
    private int _tearDownCounter = 0;

    public RetryTests()
    {
        _retryCount = 0;
        _retryMethodCallCount = 0;
    }
        
    [BDTestRetrySetUp]
    public async Task BDTestRetrySetup()
    {
        await Task.Yield();
        _setUpCounter++;
    }
        
    [BDTestRetryTearDown]
    public async Task BDTestRetryTearDown()
    {
        await Task.Yield();
        _tearDownCounter++;
    }

    private int _retryCount;

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task RetrySuccessfully(int throwIfRetryLessThan)
    {
        var scenario = await Given(() => ThrowException(throwIfRetryLessThan))
            .When(() => Console.WriteLine("my test has an exception"))
            .Then(() => Console.WriteLine("the test should retry"))
            .BDTestAsync();
            
        Assert.That(scenario.RetryCount, Is.EqualTo(throwIfRetryLessThan));
        Assert.That(scenario.Attempts, Has.Count.EqualTo(throwIfRetryLessThan + 1));
        Assert.That(Required(scenario.Attempts).Take(throwIfRetryLessThan).Select(attempt => attempt.Status),
            Is.All.EqualTo(Status.Failed));
        Assert.That(scenario.Attempts.Last().Status, Is.EqualTo(Status.Passed));
        Assert.That(_retryMethodCallCount, Is.EqualTo(throwIfRetryLessThan));
            
        Assert.That(_setUpCounter, Is.Positive);
        Assert.That(_tearDownCounter, Is.Positive);
    }

    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    public async Task RetryPastLimit(int throwIfRetryLessThan)
    {
        try
        {
            await Given(() => CheckContextCounter())
                .When(() => ThrowException(throwIfRetryLessThan))
                .Then(() => Console.WriteLine("the test should retry"))
                .BDTestAsync();

            Assert.Fail();
        }
        catch (MyCustomRetryException)
        {
            Assert.That(_retryMethodCallCount, Is.EqualTo(3));
            Assert.Pass();
        }
    }

    private void CheckContextCounter()
    {
        Assert.That(Context.ContextCounter, Is.Zero);
            
        Context.ContextCounter++;
    }

    private void ThrowException(int throwIfRetryLessThan)
    {
        if (_retryCount++ < throwIfRetryLessThan)
        {
            throw new MyCustomRetryException("Blah");
        }
    }

    public override Task OnBeforeRetry()
    {
        _retryMethodCallCount++;
        return Task.CompletedTask;
    }
}

public class RetryContext
{
    public int ContextCounter { get; set; }
}

public class MyCustomRetryException : Exception
{
    public MyCustomRetryException(string message) : base(message)
    {
    }
}