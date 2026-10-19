using BDTest.Attributes;
using BDTest.TUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that specific exceptions can be marked as 'skippable'",
    SoThat = "BDTest classifies skip and inconclusive exceptions")]
[NotInParallel]
public class IgnoreTests : RunTUnitTestBase<MyTestContext>
{
    public IgnoreTests() => Options.InconclusiveExceptionTypes.Add(typeof(MyCustomIgnoreException));
    [Test]
    public async Task TUnitSkipException()
    {
        var builder = Given(() => Console.WriteLine("A framework or custom exception signals an ignored outcome"))
            .When(() => Skip.Test("Ignored"))
            .Then(() => Console.WriteLine("the test is ignored"));

        try
        {
            await builder.BDTestAsync();
                
            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch(global::TUnit.Core.Exceptions.SkipTestException)
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
                
            await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);
        }
    }
        
    [Test]
    public async Task CustomIgnoreException()
    {
        var builder = Given(() => Console.WriteLine("A framework or custom exception signals an ignored outcome"))
            .When(() => ThrowException())
            .Then(() => Console.WriteLine("the test is ignored"));

        try
        {
            await builder.BDTestAsync();
                
            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch(MyCustomIgnoreException)
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
                
            await Assert.That(scenario.Status).IsEqualTo(Status.Inconclusive);
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
