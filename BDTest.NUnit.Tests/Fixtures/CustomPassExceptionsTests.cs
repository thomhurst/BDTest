using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CustomPassExceptionsTests : NUnitBDTestBase<MyTestContext>, IDisposable
{
    private readonly BDTestRun _run;

    public CustomPassExceptionsTests()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(OutcomeMap);
        _run = BDTestRun.Start(options);
    }

    public override BDTestRun TestRun => _run;

    public void Dispose()
    {
        try { EndTest(); }
        finally { _run.Dispose(); }
    }
    [Test]
    public async Task NUNitSuccessException()
    {
        var builder = Given(() => Console.WriteLine("NUnit throws a success exception"))
            .When(() => Assert.Pass())
            .Then(() => Console.WriteLine("the test has passed"));

        try
        {
            await builder.BDTestAsync();

            Assert.Fail("An exception should be thrown to stop us getting here!");
        }
        catch (SuccessException)
        {
            var scenario = BDTestUtil.GetScenarios(TestRun).Single();
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }
    }

}
