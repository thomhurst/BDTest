using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class AsyncScenarioMetadataTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID, global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method.MethodInfo);

    [TearDown]
    public void FinishExecution() => EndTest();

    [SetUp]
    public void Setup() => TestResetHelper.ResetData();

    [Test]
    public async Task Unannotated_test_keeps_its_name_after_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Unannotated test keeps its name after suspension"));
        Assert.That(scenario.CustomTestInformation, Is.Empty);
    }

    [Test]
    [ScenarioText("Explicit text after suspension")]
    public async Task Explicit_scenario_text_survives_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Explicit text after suspension"));
    }

    [Test]
    [TestInformation("First piece of information")]
    [CustomInformation("Custom information")]
    public async Task Test_information_survives_suspension()
    {
        await Task.Yield();

        var scenario = await Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.CustomTestInformation.Select(attribute => attribute.Print()), Is.EquivalentTo(new[]
        {
            "TestInformationAttribute - First piece of information",
            "CustomInformationAttribute - Custom information"
        }));
    }

    [Test]
    [ScenarioText("Attribute text")]
    public async Task Fluent_scenario_text_overrides_attribute_after_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp())
            .WithScenarioText("Fluent text after suspension").BDTestAsync();

        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Fluent text after suspension"));
    }

    [TestCase(1)]
    [ScenarioText("Integer overload")]
    [TestInformation("Integer information")]
    public async Task Overloaded_test_keeps_its_metadata_after_suspension(int value)
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Integer overload"));
        Assert.That(scenario.CustomTestInformation.Select(attribute => attribute.Print()),
            Is.EqualTo(new[] { "TestInformationAttribute - Integer information" }));
    }

    [TestCase("value")]
    [ScenarioText("String overload")]
    [TestInformation("String information")]
    public async Task Overloaded_test_keeps_its_metadata_after_suspension(string value)
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.GetScenarioText(), Is.EqualTo("String overload"));
        Assert.That(scenario.CustomTestInformation.Select(attribute => attribute.Print()),
            Is.EqualTo(new[] { "TestInformationAttribute - String information" }));
    }

    private static void NoOp() { }
}
