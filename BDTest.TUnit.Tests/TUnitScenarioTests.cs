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
    public async Task SkippedScenarioIsInconclusive()
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

            await Assert.That(scenario.Status).IsEqualTo(Status.Inconclusive);
        }
    }
}
