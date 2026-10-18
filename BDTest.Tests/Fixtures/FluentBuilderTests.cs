using BDTest.Attributes;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Test.Steps.Given;
using BDTest.Test.Steps.When;
using BDTest.Test.Steps.Then;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class FluentBuilderTests : BDTestBase
{
    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID,
        Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
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

        Assert.That(then.GetScenarioText(), Is.EqualTo("first"));
        Assert.That(andThen.GetScenarioText(), Is.EqualTo("second"));
        Assert.That(butThen.GetScenarioText(), Is.EqualTo("final"));
        Assert.That(scenario.GetScenarioText(), Is.EqualTo("continued"));
        Assert.That(Required(scenario.Steps).Select(step => step.StepType), Is.EqualTo(new[]
        {
            StepType.Given, StepType.AndGiven, StepType.ButGiven, StepType.When,
            StepType.AndWhen, StepType.ButWhen, StepType.Then, StepType.AndThen, StepType.ButThen, StepType.AndThen
        }));
        Assert.That(scenario.Steps.Take(9).Select(step => step.StepText), Is.EqualTo(new[]
        {
            "Given given", "And and given", "But but given", "When when", "And and when", "But but when",
            "Then then", "And and then", "But but then"
        }));
    }

    [Test]
    public async Task Scenario_text_can_precede_a_continuation()
    {
        var scenario = await When(() => NoOp()).Then(() => NoOp()).WithScenarioText("named")
            .And(() => NoOp()).But(() => NoOp());
        Assert.That(scenario.GetScenarioText(), Is.EqualTo("named"));
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
        Assert.That(reads, Is.Zero);
        var scenario = await builder;
        Assert.That(reads, Is.EqualTo(9));
        Assert.That(Required(scenario.Steps).Select(step => step.StepText), Is.EqualTo(new[]
        {
            "Given value 1", "And value 2", "But value 3", "When value 4", "And value 5", "But value 6",
            "Then value 7", "And value 8", "But value 9"
        }));
    }

    [Test]
    public void Step_kinds_are_sealed_and_customizers_have_no_hidden_or_generic_variants()
    {
        Type[] kinds = [typeof(Given), typeof(AndGiven), typeof(ButGiven), typeof(When), typeof(AndWhen),
            typeof(ButWhen), typeof(Then), typeof(AndThen), typeof(ButThen)];
        foreach (var kind in kinds)
        {
            Assert.That(kind.IsSealed, Is.True, kind.Name);
            Assert.That(kinds.Where(other => other != kind).Any(other => other.IsAssignableFrom(kind)), Is.False, kind.Name);
            foreach (var name in new[] { "WithArgs", "WithStepText", "WithScenarioText" })
            {
                if (name == "WithScenarioText" && !typeof(RunnableStepBuilder).IsAssignableFrom(kind)) continue;
                var methods = kind.GetMethods().Where(method => method.Name == name).ToArray();
                Assert.That(methods, Has.Length.EqualTo(1), kind.Name + "." + name);
                Assert.That(methods[0].ReturnType, Is.EqualTo(kind));
                Assert.That(methods[0].IsGenericMethod, Is.False);
            }
        }
    }

    [TestCase(42)]
    public async Task Public_names_describe_report_identity_and_parameter_names(int value)
    {
        var builder = When(() => Value(value)).Then(() => NoOp());
        Assert.That(builder.ParameterNames, Is.EqualTo(new[] { "value" }));
        var pending = BDTestUtil.GetNotRunScenarios().Single(definition => definition.ScenarioId == builder.ScenarioId);
        Assert.That(pending.ParameterNames, Is.EqualTo(new[] { "value" }));
        Assert.That(BDTestUtil.CurrentReportId, Is.EqualTo(BDTestRun.Default.ReportId));
        Assert.That(typeof(BDTestUtil).GetProperty("GetCurrentReportId"), Is.Null);
        Assert.That(typeof(BDTestContext).GetProperty("CurrentTestRunnerId"), Is.Null);
        Assert.That(typeof(BuildableTest).GetProperty("Parameters"), Is.Null);
        Assert.That(typeof(BDTestBase).GetMethods().Any(method => method.Name == "WithContext"), Is.False);
        await builder;
    }

    [StepText("value {0}")]
    private static void Value(int value) { }
    private static void NoOp() { }
}
