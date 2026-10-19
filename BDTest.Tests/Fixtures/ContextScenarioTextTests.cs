using System.Runtime.CompilerServices;
using BDTest;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ContextScenarioTextTests : TUnitBDTestBase<MyTestContext>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task MissingAttributeReturnsHumanizedTestName(bool readBeforeExecution)
    {
        await AssertContextScenarioText("Missing attribute returns humanized test name", readBeforeExecution);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [ScenarioText(null)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task NullAttributeTextReturnsHumanizedTestName(bool readBeforeExecution)
    {
        await AssertContextScenarioText("Null attribute text returns humanized test name", readBeforeExecution);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [ScenarioText("")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task EmptyAttributeTextRemainsEmpty(bool readBeforeExecution)
    {
        await AssertContextScenarioText("", readBeforeExecution);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [ScenarioText("Custom scenario text")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task AttributeTextIsPreserved(bool readBeforeExecution)
    {
        await AssertContextScenarioText("Custom scenario text", readBeforeExecution);
    }

    private async Task AssertContextScenarioText(string expected, bool readBeforeExecution)
    {
        var context = BDTestContext;
        await Assert.That(context.CurrentScenario).IsNull();
        await Assert.That(context.TestBase.GetScenarioText()).IsEqualTo(expected);
        if (readBeforeExecution)
        {
            await Assert.That(context.GetScenarioText()).IsEqualTo(expected);
        }

        var scenario = await When(() => NoOp()).Then(() => NoOp())
            .WithScenarioText("Fluent scenario text")
            .BDTestAsync();

        await Assert.That(context.CurrentScenario?.Guid).IsEqualTo(scenario.Guid);
        await Assert.That(scenario.ScenarioText).IsEqualTo("Fluent scenario text");
        await Assert.That(context.GetScenarioText()).IsEqualTo(expected);
    }

    private static void NoOp()
    {
    }
}
