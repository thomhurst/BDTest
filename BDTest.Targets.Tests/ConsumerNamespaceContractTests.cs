using BDTest;
using NUnit.Framework;

namespace ConsumerNamespaceTests;

[Story(AsA = "test author", IWant = "one BDTest import", SoThat = "test code is easy to write")]
public class ConsumerNamespaceContractTests
{
    [TestCase("BDTestBase")]
    [TestCase("AbstractContextBDTestBase`1")]
    [TestCase("BDTestBuilder")]
    [TestCase("BDTestScenario`1")]
    [TestCase("BuildableTest")]
    [TestCase("RunnableStepBuilder")]
    [TestCase("ScenarioOutlineBuilder`1")]
    [TestCase("Given")]
    [TestCase("AndGiven")]
    [TestCase("ButGiven")]
    [TestCase("When")]
    [TestCase("AndWhen")]
    [TestCase("ButWhen")]
    [TestCase("Then")]
    [TestCase("AndThen")]
    [TestCase("ButThen")]
    [TestCase("StepBuilder")]
    [TestCase("GivenStepBuilder")]
    [TestCase("WhenStepBuilder")]
    [TestCase("ThenStepBuilder")]
    [TestCase("StepTextAttribute")]
    [TestCase("ScenarioTextAttribute")]
    [TestCase("StoryAttribute")]
    [TestCase("BDTestRetryAttribute")]
    [TestCase("BDTestTagAttribute")]
    public void Consumer_types_are_exported_from_the_root_namespace(string name)
    {
        var assembly = typeof(BDTest.BDTestRun).Assembly;
        Assert.That(assembly.GetType("BDTest." + name)?.IsPublic, Is.True, name);
    }

    [Test, ScenarioText("A consumer retains concrete fluent types with one import")]
    public async Task One_import_supports_concrete_chains_and_structured_arguments()
    {
        using var run = BDTestRun.Start();
        var calls = 0;
        var host = new BDTestBuilder(run);
        Given given = host.Given(() => calls++).WithStepText(() => "given");
        AndGiven andGiven = given.And(() => calls++);
        ButGiven butGiven = andGiven.But(() => calls++);
        When when = butGiven.When(() => calls++);
        AndWhen andWhen = when.And(() => calls++);
        ButWhen butWhen = andWhen.But(() => calls++);
        Then then = butWhen.Then(() => calls++);
        AndThen andThen = then.And(() => calls++);
        ButThen butThen = andThen.But(() => calls++);
        RunnableStepBuilder runnable = butThen.WithDocString(new StepDocString("details"))
            .WithStepTimeout(TimeSpan.FromSeconds(10));
        BDTest.Contracts.ScenarioResult scenario = await runnable.BDTestAsync();
        Assert.That(calls, Is.EqualTo(9));
        Assert.That(scenario.Steps, Has.Count.EqualTo(9));
    }

    [Test]
    public void No_public_step_type_uses_the_old_namespace_or_repeats_its_namespace_name()
    {
        var assembly = typeof(BDTest.BDTestRun).Assembly;
        var types = assembly.GetExportedTypes();
        Assert.That(assembly.GetType("BDTest.StepType"), Is.Null, "The result enum belongs to BDTest.Contracts.");
        Assert.That(types.Where(type => type.Namespace?.StartsWith("BDTest.Test.Steps", StringComparison.Ordinal) == true), Is.Empty);
        Assert.That(types.Where(type => type.Namespace?.Split('.').Last() == type.Name.Split('`')[0]), Is.Empty);
    }
}
