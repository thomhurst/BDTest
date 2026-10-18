using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ContextScenarioTextTests : NUnitBDTestBase<MyTestContext>
{
    private const string MissingScenarioText = "No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests";

    [TestCase(false)]
    [TestCase(true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task MissingAttributeReturnsMissingText(bool readBeforeExecution)
    {
        await AssertContextScenarioText(MissingScenarioText, readBeforeExecution);
    }

    [TestCase(false)]
    [TestCase(true)]
    [ScenarioText(null)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task NullAttributeTextReturnsMissingText(bool readBeforeExecution)
    {
        await AssertContextScenarioText(MissingScenarioText, readBeforeExecution);
    }

    [TestCase(false)]
    [TestCase(true)]
    [ScenarioText("")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task EmptyAttributeTextRemainsEmpty(bool readBeforeExecution)
    {
        await AssertContextScenarioText("", readBeforeExecution);
    }

    [TestCase(false)]
    [TestCase(true)]
    [ScenarioText("Custom scenario text")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task AttributeTextIsPreserved(bool readBeforeExecution)
    {
        await AssertContextScenarioText("Custom scenario text", readBeforeExecution);
    }

    private async Task AssertContextScenarioText(string expected, bool readBeforeExecution)
    {
        var context = BDTestContext;
        Assert.That(context.CurrentScenario, Is.Null);
        Assert.That(context.TestBase.GetScenarioText(), Is.EqualTo(expected));
        if (readBeforeExecution)
        {
            Assert.That(context.GetScenarioText(), Is.EqualTo(expected));
        }

        var scenario = await When(() => NoOp()).Then(() => NoOp())
            .WithScenarioText("Fluent scenario text")
            .BDTestAsync();

        Assert.That(context.CurrentScenario, Is.SameAs(scenario));
        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Fluent scenario text"));
        Assert.That(context.GetScenarioText(), Is.EqualTo(expected));
    }

    private static void NoOp()
    {
    }
}
