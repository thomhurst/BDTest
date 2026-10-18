using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
[BDTestTag("story")]
[BDTestTag(" shared ")]
public class TagTests : TaggedStoryBase
{
    [SetUp]
    public void Reset() => TestResetHelper.ResetData();

    [Test]
    [BDTestTag("smoke")]
    [BDTestTag("SHARED")]
    public async Task Class_and_method_tags_survive_async_execution_and_builder_extensions()
    {
        await Task.Yield();
        var builder = Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp());
        Assert.That(builder.Tags, Is.EquivalentTo(new[] { "inherited", "story", "shared", "smoke" }));

        var scenario = await builder.BDTestAsync();
        Assert.That(scenario.Tags, Is.EqualTo(builder.Tags));

        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        Assert.That(report.Scenarios.Single(item => item.Guid == scenario.Guid).Tags, Is.EqualTo(scenario.Tags));
    }

    [TestCase(1)]
    [BDTestTag("integer")]
    public async Task Overloaded_scenario_keeps_its_tags(int value)
    {
        await Task.Yield();
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.Tags, Does.Contain("integer").And.Not.Contain("string"));
    }

    [TestCase("value")]
    [BDTestTag("string")]
    public async Task Overloaded_scenario_keeps_its_tags(string value)
    {
        await Task.Yield();
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.Tags, Does.Contain("string").And.Not.Contain("integer"));
    }

    [Test]
    public async Task Untagged_scenarios_and_old_json_have_empty_tags()
    {
        var scenario = await new BDTestBuilder().When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.Tags, Is.Empty);
        Assert.That(ReportJson.DeserializeObject<Scenario>("{}").Tags, Is.Empty);
        Assert.That(ReportJson.DeserializeObject<Scenario>("{\"Tags\":null}").Tags, Is.Empty);
    }

    [Test]
    [BDTestTag("not-run")]
    public void Unexecuted_scenarios_include_tags_in_json()
    {
        var builder = When(() => NoOp()).Then(() => NoOp());
        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        Assert.That(report.NotRun.Single().Tags, Is.EqualTo(builder.Tags));
        Assert.That(report.NotRun.Single().Tags, Does.Contain("not-run"));
    }

    [Test]
    [BDTestTag("override")]
    public override async Task Inherited_method_tags_combine_with_override_tags()
    {
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.Tags, Does.Contain("base-method").And.Contain("override").And.Contain("inherited"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void Empty_tags_are_rejected(string tag) =>
        Assert.Throws<ArgumentException>(() => new BDTestTagAttribute(tag));

    private static void NoOp() { }
}

[BDTestTag("inherited")]
public abstract class TaggedStoryBase : BDTestBase
{
    [BDTestTag("base-method")]
    public virtual Task Inherited_method_tags_combine_with_override_tags() => Task.CompletedTask;
}
