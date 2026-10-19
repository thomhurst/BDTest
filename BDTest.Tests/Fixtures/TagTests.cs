using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[BDTestTag("story")]
[BDTestTag(" shared ")]
public class TagTests : TaggedStoryBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    [Test]
    [BDTestTag("smoke")]
    [BDTestTag("SHARED")]
    public async Task Class_and_method_tags_survive_async_execution_and_builder_extensions()
    {
        await Task.Yield();
        var builder = Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp());

        await Assert.That(builder.Tags).IsEquivalentTo(new[] { "inherited", "story", "shared", "smoke" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        var scenario = await builder.BDTestAsync();
        await Assert.That(scenario.Tags).IsEquivalentTo(Required(builder.Tags), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        await Assert.That(Required(report.Scenarios).Single(item => item.Guid == scenario.Guid).Tags).IsEquivalentTo(Required(scenario.Tags), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(1)]
    [BDTestTag("integer")]
    public async Task Overloaded_scenario_keeps_its_tags(int value)
    {
        await Task.Yield();
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.Tags).Contains("integer").And.DoesNotContain("string");
    }

    [Test]
    [Arguments("value")]
    [BDTestTag("string")]
    public async Task Overloaded_scenario_keeps_its_tags(string value)
    {
        await Task.Yield();
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.Tags).Contains("string").And.DoesNotContain("integer");
    }

    [Test]
    public async Task Untagged_scenarios_and_old_json_have_empty_tags()
    {
        var scenario = await new BDTestBuilder(TestRun).When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        await Assert.That(scenario.Tags).IsEmpty();
        await Assert.That(ReportJson.DeserializeObject<Scenario>("{}").Tags).IsEmpty();
        await Assert.That(ReportJson.DeserializeObject<Scenario>("{\"Tags\":null}").Tags).IsEmpty();
    }

    [Test]
    [BDTestTag("not-run")]
    public async Task Unexecuted_scenarios_include_tags_in_json()
    {
        var builder = When(() => NoOp()).Then(() => NoOp());
        var report = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        await Assert.That(Required(report.NotRun).Single().Tags).IsEquivalentTo(Required(builder.Tags), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(report.NotRun.Single().Tags).Contains("not-run");
    }

    [Test]
    [BDTestTag("override")]
    public override async Task Inherited_method_tags_combine_with_override_tags()
    {
        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.Tags).Contains("base-method").And.Contains("override").And.Contains("inherited");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" \t")]
    public async Task Empty_tags_are_rejected(string? tag) =>
        await Assert.That(() => new BDTestTagAttribute(tag!)).ThrowsExactly<ArgumentException>();

    private static void NoOp() { }
}

[BDTestTag("inherited")]
public abstract class TaggedStoryBase : RunTestBase
{
    [BDTestTag("base-method")]
    public virtual Task Inherited_method_tags_combine_with_override_tags() => Task.CompletedTask;
}
