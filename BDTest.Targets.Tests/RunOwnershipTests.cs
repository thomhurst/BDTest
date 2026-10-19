using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class RunOwnershipTests
{
    [Test]
    public async Task Converter_uses_the_registered_generic_contract_for_each_run()
    {
        var converter = new MultipleContracts();
        var objectOptions = new BDTestOptions();
        objectOptions.RegisterCustomStringConverter<object>(converter);
        var stringOptions = new BDTestOptions();
        stringOptions.RegisterCustomStringConverter<string>(converter);
        using var objectRun = BDTestRun.Start(objectOptions);
        using var stringRun = BDTestRun.Start(stringOptions);
        using var defaultRun = BDTestRun.Start();
        Assert.That(await Render(objectRun), Is.EqualTo("Given display object contract"));
        Assert.That(await Render(stringRun), Is.EqualTo("Given display string contract"));
        Assert.That(await Render(defaultRun), Is.EqualTo("Given display value"));
    }

    private static async Task<string?> Render(BDTestRun run)
    {
        var scenario = await new BDTestBuilder(run).Given(() => Display("value"))
            .WithArgs(() => "value").When(() => { }).Then(() => { }).BDTestAsync();
        return scenario.Steps![0].StepText;
    }

    public static void Display(object value) { }

    private sealed class MultipleContracts : IStepTextStringConverter<object>, IStepTextStringConverter<string>
    {
        string IStepTextStringConverter<object>.ConvertToString(object value) => "object contract";
        string IStepTextStringConverter<string>.ConvertToString(string value) => "string contract";
    }

    [Test]
    public void Standalone_hosts_require_a_live_explicit_run()
    {
        Assert.Throws<ArgumentNullException>(() => new BDTestBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => BDTestScenario.For<object>(this, null!));
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        Assert.That(first.ReportId, Is.Not.EqualTo(second.ReportId));
        Assert.That(first.Scenarios, Is.Empty);
        Assert.That(BDTestUtil.GetNotRunScenarios(first), Is.Empty);
        Assert.That(new BDTestBuilder(first).TestRun, Is.SameAs(first));
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new BDTestBuilder(first));
        Assert.Throws<ObjectDisposedException>(() => BDTestScenario.For<object>(this, first));
        Assert.That(new BDTestBuilder(second).TestRun, Is.SameAs(second));
    }

    [Test]
    public async Task Concurrent_exports_copy_only_the_selected_descriptor_and_run()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var firstDescriptor = new BDTestRunDescriptor { Tag = "first", Environment = "staging" };
        firstDescriptor.CustomProperties.Add("Build", "one");
        var secondDescriptor = new BDTestRunDescriptor { Tag = "second", BranchName = "main" };
        secondDescriptor.CustomProperties.Add("Build", "two");

        var reports = await Task.WhenAll(
            Task.Run(() => BDTestJsonHelper.GetTestJsonData(firstDescriptor, first)),
            Task.Run(() => BDTestJsonHelper.GetTestJsonData(secondDescriptor, second)));
        firstDescriptor.CustomProperties["Build"] = "changed after export";
        var firstReport = BDTestJsonHelper.Deserialize(reports[0])!;
        var secondReport = BDTestJsonHelper.Deserialize(reports[1])!;
        Assert.That(firstReport.Id, Is.EqualTo(first.ReportId));
        Assert.That(secondReport.Id, Is.EqualTo(second.ReportId));
        Assert.That(firstReport.CustomProperties!["Build"], Is.EqualTo("one"));
        Assert.That(secondReport.CustomProperties!["Build"], Is.EqualTo("two"));
        Assert.That(firstReport.BranchName, Is.Null);
        Assert.That(secondReport.Environment, Is.Null);
        Assert.That(firstReport.TestTimer!.TestsStartedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(firstReport.Scenarios, Is.Empty);
        Assert.That(BDTestHtmlHelper.GetTestHtmlReport(secondDescriptor, second),
            Does.Contain("two").And.Not.Contain("changed after export").And.Not.Contain("staging"));

        var unlabeled = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, first))!;
        Assert.That(unlabeled.CustomProperties, Is.Empty);
        Assert.That(unlabeled.Tag, Is.Null);
        Assert.That(unlabeled.Environment, Is.Null);
    }
}
