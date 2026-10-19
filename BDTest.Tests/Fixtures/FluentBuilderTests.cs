using BDTest.Attributes;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;
using BDTest.Test.Steps.Then;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class FluentBuilderTests : RunTestBase
{
    [Before(HookType.Test)]
    public void BindMetadata() => BeginTest(Required(TestContext.Current).Id,
        Required(TestContext.Current).Metadata.TestDetails.MethodMetadata.GetReflectionInfo());

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    [Test]
    public async Task Fluent_customization_preserves_every_concrete_type_and_step_kind()
    {
        Given given = Given(() => NoOp()).WithArgs().WithStepText(() => "given");
        AndGiven andGiven = given.And(() => NoOp()).WithArgs().WithStepText(() => "and given");
        ButGiven butGiven = andGiven.But(() => NoOp()).WithArgs().WithStepText(() => "but given");
        When when = butGiven.When(() => NoOp()).WithArgs().WithStepText(() => "when");
        AndWhen andWhen = when.And(() => NoOp()).WithArgs().WithStepText(() => "and when");
        ButWhen butWhen = andWhen.But(() => NoOp()).WithArgs().WithStepText(() => "but when");
        Then then = butWhen.Then(() => NoOp()).WithArgs().WithStepText(() => "then").WithScenarioText("first");
        AndThen andThen = then.And(() => NoOp()).WithScenarioText("second").WithStepText(() => "and then").WithArgs();
        ButThen butThen = andThen.But(() => NoOp()).WithStepText(() => "but then").WithArgs().WithScenarioText("final");
        var scenario = await butThen.And(() => NoOp()).WithScenarioText("continued");

        await Assert.That(then.GetScenarioText()).IsEqualTo("first");
        await Assert.That(andThen.GetScenarioText()).IsEqualTo("second");
        await Assert.That(butThen.GetScenarioText()).IsEqualTo("final");
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("continued");
        await Assert.That(Required(scenario.Steps).Select(step => step.StepType)).IsEquivalentTo(new[]
        {
            StepType.Given, StepType.AndGiven, StepType.ButGiven, StepType.When,
            StepType.AndWhen, StepType.ButWhen, StepType.Then, StepType.AndThen, StepType.ButThen, StepType.AndThen
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scenario.Steps.Take(9).Select(step => step.StepText)).IsEquivalentTo((new[]
        {
            "Given given", "And and given", "But but given", "When when", "And and when", "But but when",
            "Then then", "And and then", "But but then"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Scenario_text_can_precede_a_continuation()
    {
        var scenario = await When(() => NoOp()).Then(() => NoOp()).WithScenarioText("named")
            .And(() => NoOp()).But(() => NoOp());
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("named");
    }

    [Test]
    public async Task Argument_readers_remain_lazy_across_all_step_kinds()
    {
        var reads = 0;
        var builder = Given(() => Value(0)).WithArgs(() => ++reads)
            .And(() => Value(0)).WithArgs(() => ++reads)
            .But(() => Value(0)).WithArgs(() => ++reads)
            .When(() => Value(0)).WithArgs(() => ++reads)
            .And(() => Value(0)).WithArgs(() => ++reads)
            .But(() => Value(0)).WithArgs(() => ++reads)
            .Then(() => Value(0)).WithArgs(() => ++reads)
            .And(() => Value(0)).WithArgs(() => ++reads)
            .But(() => Value(0)).WithArgs(() => ++reads);
        await Assert.That(reads).IsZero();
        var scenario = await builder;
        await Assert.That(reads).IsEqualTo(9);
        await Assert.That(Required(scenario.Steps).Select(step => step.StepText)).IsEquivalentTo((new[]
        {
            "Given value 1", "And value 2", "But value 3", "When value 4", "And value 5", "But value 6",
            "Then value 7", "And value 8", "But value 9"
        }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Step_kinds_are_sealed_and_customizers_have_no_hidden_or_generic_variants()
    {
        Type[] kinds = [typeof(Given), typeof(AndGiven), typeof(ButGiven), typeof(When), typeof(AndWhen),
            typeof(ButWhen), typeof(Then), typeof(AndThen), typeof(ButThen)];
        foreach (var kind in kinds)
        {
            await Assert.That(kind.IsSealed).IsTrue().Because(kind.Name);
            await Assert.That(kinds.Where(other => other != kind).Any(other => other.IsAssignableFrom(kind))).IsFalse().Because(kind.Name);
            foreach (var name in new[] { "WithArgs", "WithStepText", "WithScenarioText" })
            {
                if (name == "WithScenarioText" && !typeof(RunnableStepBuilder).IsAssignableFrom(kind)) continue;
                var methods = kind.GetMethods().Where(method => method.Name == name).ToArray();

                await Assert.That((methods).Length).IsEqualTo(1).Because(kind.Name + "." + name);
                await Assert.That(methods[0].ReturnType).IsEqualTo(kind);
                await Assert.That(methods[0].IsGenericMethod).IsFalse();
            }
        }
    }

    [Test]
    [Arguments(42)]
    public async Task Public_names_describe_report_identity_and_parameter_names(int value)
    {
        var builder = When(() => Value(value)).Then(() => NoOp());
        await Assert.That(builder.ParameterNames).IsEquivalentTo((new[] { "value" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var pending = BDTestUtil.GetNotRunScenarios(TestRun).Single(definition => definition.ScenarioId == builder.ScenarioId);
        await Assert.That(pending.ParameterNames).IsEquivalentTo((new[] { "value" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(builder.ReportId).IsEqualTo(TestRun.ReportId);
        await Assert.That(typeof(BDTestUtil).GetProperty("GetCurrentReportId")).IsNull();
        await Assert.That(typeof(BDTestContext).GetProperty("CurrentTestRunnerId")).IsNull();
        await Assert.That(typeof(BuildableTest).GetProperty("Parameters")).IsNull();
        await Assert.That(typeof(BDTestBase).GetMethods().Any(method => method.Name == "WithContext")).IsFalse();
        await builder;
    }

    [StepText("value {0}")]
    private static void Value(int value) { }
    private static void NoOp() { }
}
