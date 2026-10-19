using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that exceptions are logged properly",
    SoThat = "developers can see why their test failed")]
public class ExceptionTests : RunTestBase
{
    [Test]
    [Arguments(typeof(Exception), Status.Failed)]
    [Arguments(typeof(NotImplementedException), Status.NotImplemented)]
    public async Task ThrowsExceptionOnGiven(Type exceptionType, Status status)
    {
        var builder = Given(() => ThrowException(exceptionType))
            .When(() => Console.WriteLine("my test has an exception"))
            .Then(() => Console.WriteLine("the exception should be serialized to the json output"))
            .And(() => Console.WriteLine("the step is marked as failed"))
            .And(() => Console.WriteLine("the scenario is marked as failed"))
            .And(() => Console.WriteLine("subsequent steps are not run"));

        try
        {
            await builder.BDTestAsync();

            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
            var json = Required(JsonHelper.GetTestDynamicJsonObject(TestRun)["Scenarios"])
                .Single(item => (string?)item["Guid"] == builder.Guid);

            await Assert.That(Required(Required(Required(scenario.Steps)[0].Exception).Message).Contains("BDTest Exception!")).IsTrue();

            await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(status);
            await Assert.That(Required(scenario.Steps)[1].Status).IsEqualTo(Status.NotRun);
            await Assert.That(Required(scenario.Steps)[2].Status).IsEqualTo(Status.NotRun);
            await Assert.That(Required(scenario.Steps)[3].Status).IsEqualTo(Status.NotRun);
            await Assert.That(Required(scenario.Steps)[4].Status).IsEqualTo(Status.NotRun);
            await Assert.That(Required(scenario.Steps)[5].Status).IsEqualTo(Status.NotRun);

            await Assert.That(scenario.Status).IsEqualTo(status);

            await Assert.That(Required(json.SelectToken("$.Steps[0].Exception"))
                    .ToString().Contains("BDTest Exception!")).IsTrue();

            await Assert.That(Required(json.SelectToken("$.Steps[0].Status")).ToString()).IsEqualTo(status.ToString());
            await Assert.That(Required(json.SelectToken("$.Steps[1].Status")).ToString()).IsEqualTo("NotRun");
            await Assert.That(Required(json.SelectToken("$.Steps[2].Status")).ToString()).IsEqualTo("NotRun");
            await Assert.That(Required(json.SelectToken("$.Steps[3].Status")).ToString()).IsEqualTo("NotRun");
            await Assert.That(Required(json.SelectToken("$.Steps[4].Status")).ToString()).IsEqualTo("NotRun");
            await Assert.That(Required(json.SelectToken("$.Steps[5].Status")).ToString()).IsEqualTo("NotRun");

            await Assert.That(Required(json.SelectToken("$.Status")).ToString()).IsEqualTo(status.ToString());
        }
    }

    [Test]
    [Arguments(typeof(Exception), Status.Failed)]
    [Arguments(typeof(NotImplementedException), Status.NotImplemented)]
    public async Task ThrowsExceptionOnThen(Type exceptionType, Status status)
    {
        var builder = When(() => Console.WriteLine("my test has an exception"))
            .Then(() => Console.WriteLine("the exception should be serialized to the json output"))
            .And(() => Console.WriteLine("the step is marked as failed"))
            .And(() => ThrowException(exceptionType))
            .And(() => Console.WriteLine("subsequent steps are not run"))
            .And(() => Console.WriteLine("Previous steps should be marked as passed"));

        try
        {
            await builder.BDTestAsync();

            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single(scenario => scenario.Guid == builder.Guid);
            var json = Required(JsonHelper.GetTestDynamicJsonObject(TestRun)["Scenarios"])
                .Single(item => (string?)item["Guid"] == builder.Guid);

            await Assert.That(Required(Required(Required(scenario.Steps)[3].Exception).Message).Contains("BDTest Exception!")).IsTrue();

            await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Passed);
            await Assert.That(Required(scenario.Steps)[1].Status).IsEqualTo(Status.Passed);
            await Assert.That(Required(scenario.Steps)[2].Status).IsEqualTo(Status.Passed);
            await Assert.That(Required(scenario.Steps)[3].Status).IsEqualTo(status);
            await Assert.That(Required(scenario.Steps)[4].Status).IsEqualTo(Status.NotRun);
            await Assert.That(Required(scenario.Steps)[5].Status).IsEqualTo(Status.NotRun);

            await Assert.That(scenario.Status).IsEqualTo(status);

            await Assert.That(Required(json.SelectToken("$.Steps[3].Exception"))
                    .ToString().Contains("BDTest Exception!")).IsTrue();

            await Assert.That(Required(json.SelectToken("$.Steps[0].Status")).ToString()).IsEqualTo("Passed");
            await Assert.That(Required(json.SelectToken("$.Steps[1].Status")).ToString()).IsEqualTo("Passed");
            await Assert.That(Required(json.SelectToken("$.Steps[2].Status")).ToString()).IsEqualTo("Passed");
            await Assert.That(Required(json.SelectToken("$.Steps[3].Status")).ToString()).IsEqualTo(status.ToString());
            await Assert.That(Required(json.SelectToken("$.Steps[4].Status")).ToString()).IsEqualTo("NotRun");
            await Assert.That(Required(json.SelectToken("$.Steps[5].Status")).ToString()).IsEqualTo("NotRun");

            await Assert.That(Required(json.SelectToken("$.Status")).ToString()).IsEqualTo(status.ToString());
        }
    }

    [Test]
    public async Task ThrowsCorrectExceptionOnSync()
    {
        try
        {
            await When(() => Console.WriteLine("my test has an exception"))
                .Then(() => ThrowException(typeof(MyCustomTestException)))
                .And(() => Console.WriteLine("the correct exception type should be surfaced to the user"))
                .BDTestAsync();

            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch (Exception e)
        {
            await Assert.That(e.GetType()).IsEqualTo(typeof(MyCustomTestException));
        }
    }
        
    [Test]
    public async Task ThrowsCorrectExceptionOnAsync()
    {
        try
        {
            await When(() => Console.WriteLine("my test has an exception"))
                .Then(() => Task.Run(() => ThrowException(typeof(MyCustomTestException))))
                .And(() => Console.WriteLine("the correct exception type should be surfaced to the user"))
                .BDTestAsync();

            Fail.Test("An exception should be thrown to stop us getting here!");
        }
        catch (Exception e)
        {
            await Assert.That(e.GetType()).IsEqualTo(typeof(MyCustomTestException));
        }
    }

    private static void ThrowException(Type exceptionType)
    {
        throw (Exception)Required(Activator.CreateInstance(exceptionType, "BDTest Exception!"));
    }
}

public class MyCustomTestException : Exception
{
    public MyCustomTestException(string message) : base(message)
    {
    }
}
