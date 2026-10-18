using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Test.Steps;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

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
        Assert.That(definition.Steps.Select(step => step.GetType()), Is.All.EqualTo(typeof(StepDefinition)));
        var result = await builder;
        result.Tags[0] = "changed result";
        Assert.That(definition.Tags, Is.EqualTo(new[] { "definition" }));
        Assert.That(definition.Steps[0].Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(definition.ScenarioTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(definition.Steps[0].Runnable, Is.Not.Null);
        Assert.That(Required(result.Steps)[0].Runnable, Is.Null);
        var extended = builder.And(NoOp);
        Assert.That((await extended).Status, Is.EqualTo(Status.Passed));
    }

    [Test]
    public void Pending_reports_contain_only_summaries_and_never_evaluate_delegates()
    {
        using var run = BDTestRun.Start();
        var reads = 0;
        var fixture = new Capture(run);
        var builder = fixture.When(NoOp).Then(NoOp)
            .WithArgs(() => ++reads).WithStepText(() => { reads++; return "override"; })
            .WithDocString(() => { reads++; return new StepDocString("text"); })
            .WithScenarioText("pending definition");
        var pending = BDTestUtil.GetNotRunScenarios(run).Single();
        Assert.That(pending, Is.TypeOf<ScenarioDefinitionSummary>());
        Assert.That(pending.ScenarioId, Is.EqualTo(builder.ScenarioId));
        Required(pending.Tags)[0] = "changed summary";
        Assert.That(builder.Tags, Is.EqualTo(new[] { "definition" }));
        Assert.That(run.Store.NotRun.Single().Value, Is.SameAs(builder.Definition));
        var json = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);
        var restored = Required(Required(BDTestJsonHelper.Deserialize(json)).NotRun).Single();
        Assert.That(restored.GetScenarioText(), Is.EqualTo("pending definition"));
        Assert.That(restored.Steps, Has.Count.EqualTo(2));
        Assert.That(json, Does.Not.Contain("\"Runnable\"").And.Not.Contain("\"BdTestBase\"").And.Not.Contain("\"Definition\""));
        Assert.That(reads, Is.Zero);
        builder.WithScenarioText("changed definition");
        Assert.That(pending.GetScenarioText(), Is.EqualTo("pending definition"));
        Assert.That(BDTestUtil.GetNotRunScenarios(run).Single().GetScenarioText(), Is.EqualTo("changed definition"));
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
        Assert.That(Required(result.Steps)[1].StepText, Is.EqualTo("Then original"));
        Assert.That(Required(result.Steps)[1].Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(builder.Definition.Steps[1].Timeout, Is.EqualTo(TimeSpan.FromSeconds(4)));
    }

    [Test]
    public void Builders_are_absent_from_generated_report_metadata()
    {
        Assert.That(BDTestJsonContext.Default.GetTypeInfo(typeof(BuildableTest)), Is.Null);
        Assert.That(typeof(StepDefinition).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(property => property.Name),
            Does.Not.Contain("Status").And.Not.Contain("StartTime").And.Not.Contain("Exception"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Pending_reports_do_not_reconstruct_step_text_attributes(bool configureArguments)
    {
        using var run = BDTestRun.Start();
        CountingStepTextAttribute.Constructions = 0;
        var prefix = new Capture(run).When(AttributedStep);
        if (configureArguments) prefix.WithArgs(Array.Empty<Func<object?>>());
        var builder = prefix.Then(NoOp);
        var constructions = CountingStepTextAttribute.Constructions;
        Assert.That(constructions, Is.GreaterThan(0));

        var pending = BDTestUtil.GetNotRunScenarios(run).Single();
        Assert.That(Required(pending.Steps)[0].StepText, Is.EqualTo("When cached text"));
        _ = BDTestUtil.GetNotRunScenarios(run);
        _ = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), run);
        var branch = builder.And(NoOp);
        Assert.That(BDTestUtil.GetNotRunScenarios(run).Single().ScenarioId, Is.EqualTo(branch.ScenarioId));
        Assert.That(CountingStepTextAttribute.Constructions, Is.EqualTo(constructions));
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
        Assert.That(projected, Is.TypeOf<TestInformationAttribute>());
        Assert.That(projected.Information, Is.EqualTo("bug 123"));
        Assert.That(projected.Print(), Is.EqualTo("TestInformationAttribute - bug 123"));
        summary.CustomTestInformation[0] = new TestInformationAttribute("changed");
        Assert.That(Required(builder.CustomTestInformation).Single(), Is.SameAs(information));
        Assert.That(Required(BDTestUtil.GetNotRunScenarios(run).Single().CustomTestInformation).Single().Information,
            Is.EqualTo("bug 123"));
        var result = await builder;
        Assert.That(Required(result.CustomTestInformation).Single(), Is.SameAs(information));
        Assert.That(information.Details, Is.EqualTo(new[] { "custom" }));
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
