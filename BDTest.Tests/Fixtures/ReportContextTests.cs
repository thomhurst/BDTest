using BDTest.Helpers;

namespace BDTest.Tests.Fixtures;

public class ReportContextTests
{
    [Test]
    public async Task Mapping_is_eager_and_does_not_read_the_context_during_export()
    {
        var context = new MutableContext { Label = "Original <label>" };
        var calls = 0;
        var descriptor = context.ToBDTestRunDescriptor(value =>
        {
            calls++;
            return new BDTestRunDescriptor { Tag = value.Label };
        });
        context.Label = "Changed label";
        using var run = BDTestRun.Start();
        var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(descriptor, run));
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(report.Tag).IsEqualTo("Original <label>");
        await Assert.That(html).Contains("Original &lt;label&gt;");
    }

    [Test]
    public async Task Context_does_not_select_or_mix_runs()
    {
        var context = new MutableContext();
        using var selected = BDTestRun.Start();
        using var other = BDTestRun.Start();
        await new BDTestBuilder(selected).Given(() => { }).When(() => { }).Then(() => { }).WithScenarioText("Selected scenario").BDTestAsync();
        await new BDTestBuilder(other).Given(() => { }).When(() => { }).Then(() => { }).WithScenarioText("Other scenario").BDTestAsync();

        var html = context.CreateHtmlReport(selected, _ => new BDTestRunDescriptor { Tag = "Selected metadata" });

        await Assert.That(html).Contains("Selected scenario");
        await Assert.That(html).DoesNotContain("Other scenario");
        await Assert.That(html).Contains("Selected metadata");
    }

    [Test]
    public async Task Missing_context_is_rejected_before_mapping()
    {
        MutableContext context = null!;
        await Assert.That(() => context.ToBDTestRunDescriptor(_ => throw new Exception("Must not run")))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Missing_adapter_is_rejected()
    {
        await Assert.That(() => new MutableContext().ToBDTestRunDescriptor(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Missing_run_is_rejected_before_mapping()
    {
        await Assert.That(() => new MutableContext().CreateHtmlReport(null!, _ => throw new Exception("Must not run")))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Null_adapter_result_is_rejected()
    {
        await Assert.That(() => new MutableContext().ToBDTestRunDescriptor(_ => null!))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Adapter_failure_is_propagated()
    {
        using var run = BDTestRun.Start();
        await Assert.That(() => new MutableContext().CreateHtmlReport(run, _ => throw new FormatException("Mapping failed")))
            .Throws<FormatException>();
    }

    private sealed class MutableContext
    {
        public string Label { get; set; } = "";
    }
}
