using System.Text;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Metadata;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class NullableContractTests
{
    [Test]
    public async Task Json_null_has_a_nullable_result_for_both_readers()
    {
        await Assert.That(BDTestJsonHelper.Serialize(null)).IsEqualTo("null");
        await Assert.That(BDTestJsonHelper.Deserialize("null")).IsNull();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        await Assert.That(await BDTestJsonHelper.DeserializeAsync(stream)).IsNull();
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task Historical_report_omissions_remain_null_and_collections_keep_their_defaults()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": [{ "Steps": [{}], "ExecutionException": {} }] }
            """));
        var scenario = Required(report.Scenarios).Single();
        var step = Required(scenario.Steps).Single();
        var exception = Required(scenario.Exception);

        using (Assert.Multiple())
        {
            await Assert.That(report.Id).IsNull();
            await Assert.That(report.TestTimer).IsNull();
            await Assert.That(report.NotRun).IsEmpty();
            await Assert.That(scenario.Run).IsNull();
            await Assert.That(scenario.BdTestBaseClass).IsNull();
            await Assert.That(scenario.Guid).IsNull();
            await Assert.That(scenario.TestStartupInformation).IsNull();
            await Assert.That(scenario.CustomTestInformation).IsNull();
            await Assert.That(scenario.Attempts).IsEmpty();
            await Assert.That(scenario.Tags).IsEmpty();
            await Assert.That(step.StepText).IsNull();
            await Assert.That(step.Exception).IsNull();
            await Assert.That(step.Arguments).IsEmpty();
            await Assert.That(exception.Message).IsNull();
            await Assert.That(exception.ShortTypeName).IsNull();
            await Assert.That(exception.Data).IsEmpty();
            await Assert.That(exception.InnerExceptions).IsEmpty();

        }
    }

    [Test]
    public async Task Explicit_null_report_collections_are_preserved_until_host_normalization()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": null, "NotRun": null, "CustomProperties": null }
            """));
        await Assert.That(report.Scenarios).IsNull();
        await Assert.That(report.NotRun).IsNull();
        await Assert.That(report.CustomProperties).IsNull();

        var nested = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": [{ "Steps": null, "Examples": null, "Attachments": null, "Attempts": null }] }
            """));
        var scenario = Required(nested.Scenarios).Single();
        await Assert.That(scenario.Steps).IsNull();
        await Assert.That(scenario.Examples).IsNull();
        await Assert.That(scenario.Attachments).IsNull();
        await Assert.That(scenario.Attempts).IsNull();
        await Assert.That(scenario.Exception).IsNull();
    }

    [Test]
    public async Task Optional_caller_metadata_and_null_argument_values_keep_runtime_fallbacks()
    {
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = null, Services = null });
        var host = new Host(run);
        host.BeginTest("nullable-contract", null);
        try
        {
            await Assert.That(host.Context).IsNotNull();
            await Assert.That(host.BDTestContext.CurrentScenario).IsNull();
            await Assert.That(host.GetStoryText()).IsNull();
            var scenario = await host.When(() => Accept(null), callerMember: null, callerFile: null,
                    argumentExpression: null)
                .WithArgs(() => null)
                .WithStepText(() => null)
                .Then(() => { }, callerFile: null, argumentExpression: null)
                .WithStepText(null)
                .BDTestAsync();
            await Assert.That(scenario.FileName).IsNull();
            await Assert.That(Required(scenario.Steps)[0].StepText).EndsWith("null");
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        }
        finally
        {
            await host.EndTestAsync();
        }
    }

    [Test]
    public async Task Optional_metadata_and_attribute_values_accept_null()
    {
        var metadata = new StepCallSiteMetadata(null, 0, null, null, false);
        await Assert.That(metadata.Method).IsNull();
        await Assert.That(metadata.TryBindArguments(typeof(NullableContractTests).Assembly, (Action)(() => { }),
            out var arguments)).IsFalse();
        await Assert.That(arguments).IsEmpty();
        await Assert.That(new ScenarioTextAttribute(null).Text).IsNull();
        await Assert.That(new StepTextAttribute(null).Text).IsNull();
        await Assert.That(new TestInformationAttribute(null).Information).IsNull();
    }

    private static void Accept(object? value) { }

    private sealed class Context;

    private sealed class Host(BDTestRun run) : AbstractContextBDTestBase<Context>
    {
        public override BDTestRun TestRun => run;
    }
}
