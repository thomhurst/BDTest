using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "These tests verify reporting of intentionally unexecuted scenarios.")]
[Story(AsA = "BDTest developer",
    IWant = "to make sure that BDTest flags tests not executed",
    SoThat = "consumers aren't missing any test coverage accidentally")]
public class NotRunWarnings : RunTestBase
{

    [Test]
    public async Task Test()
    {
        await SuccessTest();

        NotRunTest1();

        NotRunTest2();

        NotRunTest3();

        var notRunScenarios = BDTestUtil.GetNotRunScenarios(TestRun);
        await Assert.That(notRunScenarios.Count).IsEqualTo(3);

        await Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest1")).IsTrue();
        await Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest2")).IsTrue();
        await Assert.That(notRunScenarios.Any(scenario => scenario.GetScenarioText() == "Custom NotRunTest3")).IsTrue();

        await Assert.That(notRunScenarios.All(scenario => scenario.GetStoryText() == @"As a BDTest developer
I want to make sure that BDTest flags tests not executed
So that consumers aren't missing any test coverage accidentally
")).IsTrue();
    }

    private void NotRunTest3()
    {
        When(() => Console.WriteLine("Don't execute a test properly"))
            .Then(() => Console.WriteLine("It shows as a warning in the report."))
            .And(() => Console.WriteLine("I know that the test is")).WithScenarioText("Custom NotRunTest3");
    }

    private void NotRunTest2()
    {
        When(() => Console.WriteLine("Don't execute a test properly"))
            .Then(() => Console.WriteLine("It shows as a warning in the report.")).WithScenarioText("Custom NotRunTest2");
    }

    private void NotRunTest1()
    {
        When(() => Console.WriteLine("Don't execute a test properly")).Then(() => { }).WithScenarioText("Custom NotRunTest1");
    }

    private async Task SuccessTest()
    {
        await When(() => Console.WriteLine("Run a test successfully"))
            .Then(() => Console.WriteLine("It shows in the report successfully."))
            .BDTestAsync();
    }
}
