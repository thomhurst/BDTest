using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.None)]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that exceptions are logged properly",
    SoThat = "developers can see why their test failed")]
public class ExceptionTests : BDTestBase
{
    [TestCase(typeof(Exception), Status.Failed)]
    [TestCase(typeof(NotImplementedException), Status.NotImplemented)]
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

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
            var json = JsonHelper.GetTestDynamicJsonObject()["Scenarios"]
                .Single(item => (string)item["Guid"] == builder.Guid);

            Assert.That(scenario.Steps[0].Exception.Message.Contains("BDTest Exception!"));

            Assert.That(scenario.Steps[0].Status, Is.EqualTo(status));
            Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.NotRun));
            Assert.That(scenario.Steps[2].Status, Is.EqualTo(Status.NotRun));
            Assert.That(scenario.Steps[3].Status, Is.EqualTo(Status.NotRun));
            Assert.That(scenario.Steps[4].Status, Is.EqualTo(Status.NotRun));
            Assert.That(scenario.Steps[5].Status, Is.EqualTo(Status.NotRun));

            Assert.That(scenario.Status, Is.EqualTo(status));

            Assert.That(
                json.SelectToken("$.Steps[0].Exception")
                    .ToString().Contains("BDTest Exception!"));

            Assert.That(
                json.SelectToken("$.Steps[0].Status").ToString(),
                Is.EqualTo(status.ToString()));
            Assert.That(
                json.SelectToken("$.Steps[1].Status").ToString(),
                Is.EqualTo("NotRun"));
            Assert.That(
                json.SelectToken("$.Steps[2].Status").ToString(),
                Is.EqualTo("NotRun"));
            Assert.That(
                json.SelectToken("$.Steps[3].Status").ToString(),
                Is.EqualTo("NotRun"));
            Assert.That(
                json.SelectToken("$.Steps[4].Status").ToString(),
                Is.EqualTo("NotRun"));
            Assert.That(
                json.SelectToken("$.Steps[5].Status").ToString(),
                Is.EqualTo("NotRun"));

            Assert.That(json.SelectToken("$.Status").ToString(),
                Is.EqualTo(status.ToString()));
        }
    }

    [TestCase(typeof(Exception), Status.Failed)]
    [TestCase(typeof(NotImplementedException), Status.NotImplemented)]
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

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch
        {
            var scenario = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == builder.Guid);
            var json = JsonHelper.GetTestDynamicJsonObject()["Scenarios"]
                .Single(item => (string)item["Guid"] == builder.Guid);

            Assert.That(scenario.Steps[3].Exception.Message.Contains("BDTest Exception!"));

            Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.Steps[2].Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.Steps[3].Status, Is.EqualTo(status));
            Assert.That(scenario.Steps[4].Status, Is.EqualTo(Status.NotRun));
            Assert.That(scenario.Steps[5].Status, Is.EqualTo(Status.NotRun));

            Assert.That(scenario.Status, Is.EqualTo(status));

            Assert.That(
                json.SelectToken("$.Steps[3].Exception")
                    .ToString().Contains("BDTest Exception!"));

            Assert.That(
                json.SelectToken("$.Steps[0].Status").ToString(),
                Is.EqualTo("Passed"));
            Assert.That(
                json.SelectToken("$.Steps[1].Status").ToString(),
                Is.EqualTo("Passed"));
            Assert.That(
                json.SelectToken("$.Steps[2].Status").ToString(),
                Is.EqualTo("Passed"));
            Assert.That(
                json.SelectToken("$.Steps[3].Status").ToString(),
                Is.EqualTo(status.ToString()));
            Assert.That(
                json.SelectToken("$.Steps[4].Status").ToString(),
                Is.EqualTo("NotRun"));
            Assert.That(
                json.SelectToken("$.Steps[5].Status").ToString(),
                Is.EqualTo("NotRun"));

            Assert.That(json.SelectToken("$.Status").ToString(),
                Is.EqualTo(status.ToString()));
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

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (Exception e)
        {
            Assert.That(e.GetType(), Is.EqualTo(typeof(MyCustomTestException)));
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

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (Exception e)
        {
            Assert.That(e.GetType(), Is.EqualTo(typeof(MyCustomTestException)));
        }
    }

    private static void ThrowException(Type exceptionType)
    {
        throw (Exception) Activator.CreateInstance(exceptionType, "BDTest Exception!");
    }
}

public class MyCustomTestException : Exception
{
    public MyCustomTestException(string message) : base(message)
    {
    }
}