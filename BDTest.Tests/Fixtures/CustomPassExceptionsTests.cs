using BDTest.Attributes;
using BDTest.TUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that specific exceptions can be registered as 'passable'",
    SoThat = "BDTest classifies registered success exceptions")]
public class CustomPassExceptionsTests : RunTUnitTestBase<MyTestContext>
{
    public CustomPassExceptionsTests() => Options.SuccessExceptionTypes.Add(typeof(MyAllowedException));

    [Test]
    public async Task CustomException()
    {

        var builder = Given(() => Console.WriteLine("A custom exception signals success"))
            .When(() => ThrowException())
            .Then(() => Console.WriteLine("the test has passed"));

        try
        {
            await builder.BDTestAsync();

            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch (MyAllowedException)
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
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
