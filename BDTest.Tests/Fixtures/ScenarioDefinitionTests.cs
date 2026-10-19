using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Test.Steps;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ScenarioDefinitionTests
{
    [Test]
    public async Task Definitions_hold_no_execution_results_and_survive_execution()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run);
        var builder = fixture.When(NoOp).WithStepTimeout(TimeSpan.FromSeconds(3))
            .Then(NoOp).WithScenarioTimeout(TimeSpan.FromSeconds(5));
        var definition = builder.Definition;

        await Assert.That((definition.Steps.Select(step => step.GetType())).All(item => Equals(item, typeof(StepDefinition)))).IsTrue();
        var result = await builder;
        run.Store.ScenariosByInternalId[result.Guid].Tags[0] = "changed result";
        await Assert.That(definition.Tags).IsEquivalentTo(new[] { "definition" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(definition.Steps[0].Timeout).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(definition.ScenarioTimeout).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(definition.Steps[0].Runnable).IsNotNull();
        await Assert.That(run.Store.ScenariosByInternalId[result.Guid].ExecutionSteps[0].Runnable).IsNull();
        var extended = builder.And(NoOp);
        await Assert.That((await extended).Status).IsEqualTo(Status.Passed);
    }

    [Test]
    public async Task Pending_reports_contain_only_summaries_and_never_evaluate_delegates()
    {
        using var run = BDTestRun.Start();
        var reads = 0;
        var fixture = new Capture(run);
        var builder = fixture.When(NoOp).Then(NoOp)
            .WithArgs(() => ++reads).WithStepText(() => { reads++; return "override"; })
            .WithDocString(() => { reads++; return new StepDocString("text"); })
            .WithScenarioText("pending definition");
        var pending = BDTestUtil.GetNotRunScenarios(run).Single();
        await Assert.That(pending).IsTypeOf<BDTest.Contracts.ScenarioDefinitionSummary>();
        await Assert.That(pending.ScenarioId).IsEqualTo(builder.ScenarioId);
        await Assert.That(() => ((IList<string>)pending.Tags)[0] = "changed summary").ThrowsExactly<NotSupportedException>();
        await Assert.That(builder.Tags).IsEquivalentTo(new[] { "definition" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(run.Store.NotRun.Single().Value).IsSameReferenceAs(builder.Definition);
        var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);
        var restored = Required(Required(BDTestJsonHelper.DeserializeReport(json)).NotRun).Single();
        await Assert.That(restored.ScenarioText).IsEqualTo("pending definition");
        await Assert.That(restored.Steps).Count().IsEqualTo(2);

        await Assert.That(json).DoesNotContain("\"Runnable\"").And.DoesNotContain("\"BdTestBase\"").And.DoesNotContain("\"Definition\"");
        await Assert.That(reads).IsZero();
        _ = builder.WithScenarioText("changed definition");
        await Assert.That(pending.ScenarioText).IsEqualTo("pending definition");
        await Assert.That(BDTestUtil.GetNotRunScenarios(run).Single().ScenarioText).IsEqualTo("changed definition");
    }

    [Test]
    public async Task Configuration_changes_after_start_do_not_change_active_execution()
    {
        using var run = BDTestRun.Start();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Capture(run);
        var builder = fixture.When(async () => { started.SetResult(); await resume.Task; })
            .Then(NoOp).WithStepText(() => "original").WithStepTimeout(TimeSpan.FromSeconds(3));
        var execution = builder.BDTestAsync();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            _ = builder.WithStepText(() => "changed").WithStepTimeout(TimeSpan.FromSeconds(4));
        }
        finally
        {
            resume.TrySetResult();
        }
        var result = await execution;
        await Assert.That(Required(result.Steps)[1].StepText).IsEqualTo("Then original");
        await Assert.That(Required(result.Steps)[1].Timeout).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(builder.Definition.Steps[1].Timeout).IsEqualTo(TimeSpan.FromSeconds(4));
    }

    [Test]
    public async Task Builders_are_absent_from_generated_report_metadata()
    {
        await Assert.That(BDTestJsonContext.Default.GetTypeInfo(typeof(BuildableTest))).IsNull();

        await Assert.That(typeof(StepDefinition).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(property => property.Name)).DoesNotContain("Status").And.DoesNotContain("StartTime").And.DoesNotContain("Exception");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Pending_reports_do_not_reconstruct_step_text_attributes(bool configureArguments)
    {
        using var run = BDTestRun.Start();
        CountingStepTextAttribute.Constructions = 0;
        var prefix = new Capture(run).When(AttributedStep);
        if (configureArguments) prefix.WithArgs(Array.Empty<Func<object?>>());
        var builder = prefix.Then(NoOp);
        var constructions = CountingStepTextAttribute.Constructions;
        await Assert.That(constructions).IsGreaterThan(0);

        var pending = BDTestUtil.GetNotRunScenarios(run).Single();
        await Assert.That(Required(pending.Steps)[0].StepText).IsEqualTo("When cached text");
        _ = BDTestUtil.GetNotRunScenarios(run);
        _ = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);
        var branch = builder.And(NoOp);
        await Assert.That(BDTestUtil.GetNotRunScenarios(run).Single().ScenarioId).IsEqualTo(branch.ScenarioId);
        await Assert.That(CountingStepTextAttribute.Constructions).IsEqualTo(constructions);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Execution_uses_cached_templates_before_running_overridden_steps(bool configureArguments)
    {
        using var run = BDTestRun.Start();
        ThrowingStepTextAttribute.ThrowOnConstruction = false;
        var prefix = new Capture(run).When(ThrowingAttributedStep);
        var argumentReads = 0;
        if (configureArguments) prefix.WithArgs(() => ++argumentReads);
        var overrideReads = 0;
        prefix.WithStepText(() => { overrideReads++; return "override"; });
        var builder = prefix.Then(NoOp);
        ThrowingStepTextAttribute.ThrowOnConstruction = true;
        try
        {
            var definition = builder.Definition.Steps[0];
            var step = new Step(definition, builder.ScenarioId, "report", run);
            await Assert.That(step.StepText).IsEqualTo("When cached text");
            await Assert.That(overrideReads).IsZero();
            if (configureArguments)
            {
                await Assert.That(step.ExplicitArguments).Count().IsEqualTo(1);
                await Assert.That(step.ExplicitArguments).IsNotSameReferenceAs(definition.ExplicitArguments);
            }
            var result = await builder;
            await Assert.That(result.Status).IsEqualTo(Status.Passed);
            await Assert.That(Required(result.Steps)[0].StepText).IsEqualTo("When override");
            await Assert.That(overrideReads).IsEqualTo(1);
            await Assert.That(argumentReads).IsZero();
        }
        finally
        {
            ThrowingStepTextAttribute.ThrowOnConstruction = false;
        }
    }

    [ThrowingStepText]
    private static void ThrowingAttributedStep() { }

    private sealed class ThrowingStepTextAttribute : StepTextAttribute
    {
        internal static bool ThrowOnConstruction;
        public ThrowingStepTextAttribute() : base("cached text")
        {
            if (ThrowOnConstruction) throw new InvalidOperationException("Attribute reconstructed during execution");
        }
    }

    [Test]
    public async Task Pending_information_is_an_independent_data_only_projection()
    {
        using var run = BDTestRun.Start();
        var builder = new Capture(run).When(NoOp).Then(NoOp);
        var information = new CustomInformationAttribute("bug 123") { Details = new[] { "custom" } };
        builder.CustomTestInformation = new TestInformationAttribute[] { information };

        var summary = BDTestUtil.GetNotRunScenarios(run).Single();
        var projected = Required(summary.CustomTestInformation).Single();
        await Assert.That(projected).IsTypeOf<string>();
        await Assert.That(projected).IsEqualTo("bug 123");
        await Assert.That(() => ((IList<string>)summary.CustomTestInformation)[0] = "changed").ThrowsExactly<NotSupportedException>();
        await Assert.That(Required(builder.CustomTestInformation).Single()).IsSameReferenceAs(information);
        await Assert.That(Required(BDTestUtil.GetNotRunScenarios(run).Single().CustomTestInformation).Single()).IsEqualTo("bug 123");
        var result = await builder;
        await Assert.That(Required(result.CustomTestInformation).Single()).IsEqualTo(information.Information);
        await Assert.That(information.Details).IsEquivalentTo(new[] { "custom" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [CountingStepText]
    private static void AttributedStep() { }

    private sealed class CountingStepTextAttribute : StepTextAttribute
    {
        internal static int Constructions;
        public CountingStepTextAttribute() : base("cached text") => Constructions++;
    }

    private sealed class CustomInformationAttribute(string information) : TestInformationAttribute(information)
    {
        public string[] Details { get; set; } = Array.Empty<string>();
    }

    private static void NoOp() { }

    [BDTestTag("definition")]
    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
