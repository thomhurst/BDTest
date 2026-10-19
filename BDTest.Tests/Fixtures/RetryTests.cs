using BDTest;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
[BDTestRetry(3)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that tests can be retried",
    SoThat = "developers can mitigate against flakey tests")]
public class RetryTests : TUnitBDTestBase<RetryContext>
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
    private async Task BDTestRetrySetup()
    {
        await Task.Yield();
        _setUpCounter++;
    }
        
    [BDTestRetryTearDown]
    private async Task BDTestRetryTearDown()
    {
        await Task.Yield();
        _tearDownCounter++;
    }

    private int _retryCount;

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task RetrySuccessfully(int throwIfRetryLessThan)
    {
        var scenario = await Given(() => ThrowException(throwIfRetryLessThan))
            .When(() => Console.WriteLine("my test has an exception"))
            .Then(() => Console.WriteLine("the test should retry"))
            .BDTestAsync();
            
        await Assert.That(scenario.RetryCount).IsEqualTo(throwIfRetryLessThan);
        await Assert.That(scenario.Attempts).Count().IsEqualTo(throwIfRetryLessThan + 1);

        await Assert.That((Required(scenario.Attempts).Take(throwIfRetryLessThan).Select(attempt => attempt.Status)).All(item => Equals(item, Status.Failed))).IsTrue();
        await Assert.That(scenario.Attempts.Last().Status).IsEqualTo(Status.Passed);
        await Assert.That(_retryMethodCallCount).IsEqualTo(throwIfRetryLessThan);

        await Assert.That(_setUpCounter).IsPositive();
        await Assert.That(_tearDownCounter).IsPositive();
    }

    [Test]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task RetryPastLimit(int throwIfRetryLessThan)
    {
        try
        {
            await Given(() => CheckContextCounter())
                .When(() => ThrowException(throwIfRetryLessThan))
                .Then(() => Console.WriteLine("the test should retry"))
                .BDTestAsync();

            Fail.Test("");
        }
        catch (MyCustomRetryException)
        {
            await Assert.That(_retryMethodCallCount).IsEqualTo(3);

        }
    }

    private async Task CheckContextCounter()
    {
        await Assert.That(Context.ContextCounter).IsZero();

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
