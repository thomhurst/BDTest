using System.Text;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Metadata;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class NullableContractTests
{
    [Test]
    public async Task Json_null_has_a_nullable_result_for_both_readers()
    {
        Assert.That(BDTestJsonHelper.Serialize(null), Is.EqualTo("null"));
        Assert.That(BDTestJsonHelper.Deserialize("null"), Is.Null);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        Assert.That(await BDTestJsonHelper.DeserializeAsync(stream), Is.Null);
        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public void Historical_report_omissions_remain_null_and_collections_keep_their_defaults()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": [{ "Steps": [{}], "ExecutionException": {} }] }
            """));
        var scenario = Required(report.Scenarios).Single();
        var step = Required(scenario.Steps).Single();
        var exception = Required(scenario.Exception);

        Assert.Multiple(() =>
        {
            Assert.That(report.Id, Is.Null);
            Assert.That(report.TestTimer, Is.Null);
            Assert.That(report.NotRun, Is.Empty);
            Assert.That(scenario.Run, Is.Null);
            Assert.That(scenario.BdTestBaseClass, Is.Null);
            Assert.That(scenario.Guid, Is.Null);
            Assert.That(scenario.TestStartupInformation, Is.Null);
            Assert.That(scenario.CustomTestInformation, Is.Null);
            Assert.That(scenario.Attempts, Is.Empty);
            Assert.That(scenario.Tags, Is.Empty);
            Assert.That(step.StepText, Is.Null);
            Assert.That(step.Exception, Is.Null);
            Assert.That(step.Arguments, Is.Empty);
            Assert.That(exception.Message, Is.Null);
            Assert.That(exception.ShortTypeName, Is.Null);
            Assert.That(exception.Data, Is.Empty);
            Assert.That(exception.InnerExceptions, Is.Empty);
        });
    }

    [Test]
    public void Explicit_null_report_collections_are_preserved_until_host_normalization()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": null, "NotRun": null, "CustomProperties": null }
            """));
        Assert.That(report.Scenarios, Is.Null);
        Assert.That(report.NotRun, Is.Null);
        Assert.That(report.CustomProperties, Is.Null);

        var nested = Required(BDTestJsonHelper.Deserialize("""
            { "Scenarios": [{ "Steps": null, "Examples": null, "Attachments": null, "Attempts": null }] }
            """));
        var scenario = Required(nested.Scenarios).Single();
        Assert.That(scenario.Steps, Is.Null);
        Assert.That(scenario.Examples, Is.Null);
        Assert.That(scenario.Attachments, Is.Null);
        Assert.That(scenario.Attempts, Is.Null);
        Assert.That(scenario.Exception, Is.Null);
    }

    [Test]
    public async Task Optional_caller_metadata_and_null_argument_values_keep_runtime_fallbacks()
    {
        using var run = BDTestRun.Start(new BDTestOptions { ContextFactory = null, Services = null });
        var host = new Host(run);
        host.BeginTest("nullable-contract", null);
        try
        {
            Assert.That(host.Context, Is.Not.Null);
            Assert.That(host.BDTestContext.CurrentScenario, Is.Null);
            Assert.That(host.GetStoryText(), Is.Null);
            var scenario = await host.When(() => Accept(null), callerMember: null, callerFile: null,
                    argumentExpression: null)
                .WithArgs(() => null)
                .WithStepText(() => null)
                .Then(() => { }, callerFile: null, argumentExpression: null)
                .WithStepText(null)
                .BDTestAsync();
            Assert.That(scenario.FileName, Is.Null);
            Assert.That(Required(scenario.Steps)[0].StepText, Does.EndWith("null"));
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        }
        finally
        {
            await host.EndTestAsync();
        }
    }

    [Test]
    public void Optional_metadata_and_attribute_values_accept_null()
    {
        var metadata = new StepCallSiteMetadata(null, 0, null, null, false);
        Assert.That(metadata.Method, Is.Null);
        Assert.That(metadata.TryBindArguments(typeof(NullableContractTests).Assembly, (Action)(() => { }),
            out var arguments), Is.False);
        Assert.That(arguments, Is.Empty);
        Assert.That(new ScenarioTextAttribute(null).Text, Is.Null);
        Assert.That(new StepTextAttribute(null).Text, Is.Null);
        Assert.That(new TestInformationAttribute(null).Information, Is.Null);
    }

    private static void Accept(object? value) { }

    private sealed class Context;

    private sealed class Host(BDTestRun run) : AbstractContextBDTestBase<Context>
    {
        public override BDTestRun TestRun => run;
    }
}
