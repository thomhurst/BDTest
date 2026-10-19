using BDTest;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class AsyncScenarioMetadataTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    [Test]
    public async Task Unannotated_test_keeps_its_name_after_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.ScenarioText).IsEqualTo("Unannotated test keeps its name after suspension");
        await Assert.That(scenario.CustomTestInformation).IsEmpty();
    }

    [Test]
    [ScenarioText("Explicit text after suspension")]
    public async Task Explicit_scenario_text_survives_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.ScenarioText).IsEqualTo("Explicit text after suspension");
    }

    [Test]
    [TestInformation("First piece of information")]
    [CustomInformation("Custom information")]
    public async Task Test_information_survives_suspension()
    {
        await Task.Yield();

        var scenario = await Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp()).BDTestAsync();


        await Assert.That(Required(scenario.CustomTestInformation).Select(attribute => attribute)).IsEquivalentTo(new[]
        {
            "First piece of information",
            "Custom information"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [ScenarioText("Attribute text")]
    public async Task Fluent_scenario_text_overrides_attribute_after_suspension()
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp())
            .WithScenarioText("Fluent text after suspension").BDTestAsync();

        await Assert.That(scenario.ScenarioText).IsEqualTo("Fluent text after suspension");
    }

    [Test]
    [Arguments(1)]
    [ScenarioText("Integer overload")]
    [TestInformation("Integer information")]
    public async Task Overloaded_test_keeps_its_metadata_after_suspension(int value)
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.ScenarioText).IsEqualTo("Integer overload");
        await Assert.That(Required(scenario.CustomTestInformation).Select(attribute => attribute)).IsEquivalentTo(new[] { "Integer information" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("value")]
    [ScenarioText("String overload")]
    [TestInformation("String information")]
    public async Task Overloaded_test_keeps_its_metadata_after_suspension(string value)
    {
        await Task.Yield();

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.ScenarioText).IsEqualTo("String overload");
        await Assert.That(Required(scenario.CustomTestInformation).Select(attribute => attribute)).IsEquivalentTo(new[] { "String information" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static void NoOp() { }
}
