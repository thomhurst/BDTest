using BDTest.Test;
using TUnit.Core.Exceptions;

namespace BDTest.TUnit.Tests;

public class TUnitScenarioTests : TUnitBDTestBase<TUnitContext>
{
    [Test]
    public async Task PassingScenarioIsRecordedAgainstTUnitTest()
    {
        var scenario = await Given(() => Console.WriteLine("a TUnit test"))
            .When(() => Console.WriteLine("BDTest steps are run"))
            .Then(() => Console.WriteLine("the scenario is recorded"))
            .BDTestAsync();

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.FrameworkTestId).IsEqualTo(TestContext.Current!.Id);
    }

    [Test]
    public async Task SkippedScenarioIsSkipped()
    {
        var testId = TestContext.Current!.Id;

        try
        {
            await Given(() => Console.WriteLine("TUnit throws a skip exception"))
                .When(() => Skip.Test("Skipping"))
                .Then(() => Console.WriteLine("the test is skipped"))
                .BDTestAsync();

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (SkipTestException)
        {
            var scenario = BDTestUtil.GetScenarios().Single(s => s.FrameworkTestId == testId);

            await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);
        }
    }

    [Test]
    public async Task InconclusiveScenarioIsInconclusive()
    {
        var testId = TestContext.Current!.Id;

        try
        {
            await Given(() => Console.WriteLine("a TUnit test"))
                .When(() => ThrowInconclusiveException())
                .Then(() => Assert.Fail("Subsequent steps must not run."))
                .BDTestAsync();

            Assert.Fail("The inconclusive exception should be propagated.");
        }
        catch (InconclusiveTestException exception)
        {
            var scenario = BDTestUtil.GetScenarios().Single(s => s.FrameworkTestId == testId);

            await Assert.That(exception.Message).IsEqualTo("Inconclusive scenario");
            await Assert.That(scenario.Status).IsEqualTo(Status.Inconclusive);
            await Assert.That(scenario.Steps[0].Status).IsEqualTo(Status.Passed);
            await Assert.That(scenario.Steps[1].Status).IsEqualTo(Status.Inconclusive);
            await Assert.That(scenario.Steps[2].Status).IsEqualTo(Status.NotRun);
        }
    }

    [Test]
    public async Task FailingStepIsRecordedAndStopsSubsequentSteps()
    {
        var testId = TestContext.Current!.Id;
        var failure = new InvalidOperationException("Expected step failure");

        try
        {
            await Given(() => Console.WriteLine("before failure"))
                .When(() => ThrowFailure(failure))
                .Then(() => Assert.Fail("Subsequent steps must not run."))
                .BDTestAsync();

            Assert.Fail("The step exception should be propagated.");
        }
        catch (InvalidOperationException exception)
        {
            var scenario = BDTestUtil.GetScenarios().Single(s => s.FrameworkTestId == testId);

            await Assert.That(ReferenceEquals(exception, failure)).IsTrue();
            await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
            await Assert.That(scenario.Steps[0].Status).IsEqualTo(Status.Passed);
            await Assert.That(scenario.Steps[1].Status).IsEqualTo(Status.Failed);
            await Assert.That(scenario.Steps[1].Exception.Message).IsEqualTo(failure.Message);
            await Assert.That(scenario.Steps[1].Exception.TypeName).IsEqualTo(typeof(InvalidOperationException).FullName);
            await Assert.That(scenario.Steps[1].Exception.ShortTypeName).IsEqualTo(nameof(InvalidOperationException));
            await Assert.That(scenario.Steps[2].Status).IsEqualTo(Status.NotRun);
            await Assert.That(scenario.Steps[2].StartTime).IsEqualTo(default(DateTime));
        }
    }

    private static void ThrowFailure(Exception exception) => throw exception;

    private static void ThrowInconclusiveException() => throw new InconclusiveTestException("Inconclusive scenario");
}
