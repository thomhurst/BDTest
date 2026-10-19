using BDTest.Contracts;
using BDTest.Helpers;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class ReportingBoundaryTests
{
    [TestCase(typeof(BDTestHtmlHelper))]
    [TestCase(typeof(BDTestJsonHelper))]
    [TestCase(typeof(BDTestReportServer))]
    [TestCase(typeof(BDTestRunDescriptor))]
    [TestCase(typeof(BDTestAttachmentHelper))]
    [TestCase(typeof(ExceptionHtmlHelper))]
    [TestCase(typeof(StepArgumentHtmlHelper))]
    public void Export_APIs_ship_in_the_reporting_assembly(Type type)
    {
        Assert.That(type.Assembly.GetName().Name, Is.EqualTo("BDTest.Reporting"));
        Assert.That(type.Assembly.GetName().GetPublicKey(), Is.Not.Empty);
        Assert.That(typeof(BDTestRun).Assembly.GetType(type.FullName!), Is.Null);
    }

    [Test]
    public void Core_has_no_reporting_dependency_and_keeps_capture_and_legacy_metadata()
    {
        var core = typeof(BDTestRun).Assembly;
        Assert.That(core.GetReferencedAssemblies().Select(assembly => assembly.Name),
            Does.Not.Contain("BDTest.Reporting"));
        Assert.That(typeof(HtmlWriter).Assembly, Is.SameAs(core));
        Assert.That(typeof(BDTestJsonContext).Assembly, Is.SameAs(core));
    }

    [Test]
    public async Task Snapshot_is_isolated_and_retained_after_the_run_is_cleared_and_disposed()
    {
        using var run = BDTestRun.Start();
        using var other = BDTestRun.Start();
        var builder = new BDTestBuilder(run);
        var result = await builder.When(() => { }).Then(() => { }).BDTestAsync();
        var pending = builder.When(() => { }).Then(() => { });
        var snapshot = run.GetSnapshot();
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run));
        Assert.That(snapshot.ReportId, Is.EqualTo(run.ReportId));
        Assert.That(snapshot.Scenarios.Single().Guid, Is.EqualTo(result.Guid));
        Assert.That(snapshot.NotRun.Count, Is.EqualTo(1));
        Assert.That(snapshot.Timer.TestsStartedAt, Is.EqualTo(report.TestTimer!.TestsStartedAt));
        Assert.That(snapshot.Version, Is.EqualTo(report.ProducerVersion));
        Assert.That(other.GetSnapshot().Scenarios, Is.Empty);
        BDTestUtil.ClearScenarios(run);
        run.Dispose();
        Assert.That(snapshot.Scenarios.Count, Is.EqualTo(1));
        Assert.That(snapshot.NotRun.Count, Is.EqualTo(1));
        Assert.Throws<NotSupportedException>(() => ((IList<ScenarioResult>)snapshot.Scenarios).Clear());
        Assert.Throws<ObjectDisposedException>(() => run.GetSnapshot());
        Assert.Throws<ObjectDisposedException>(() => BDTestJsonHelper.GetTestJsonData(null, run));
        Assert.Throws<ObjectDisposedException>(() => BDTestHtmlHelper.GetTestHtmlReport(null, run));
        GC.KeepAlive(pending);
    }

    [Test]
    public void Legacy_rendering_uses_the_supported_immutable_conversion()
    {
        var exception = new ExceptionWrapper(new InvalidOperationException("original"));
        var snapshot = exception.ToResult();
        exception.Message = "changed";
        Assert.That(snapshot.Message, Is.EqualTo("original"));
        Assert.That(ExceptionHtmlHelper.GetHtml(exception), Is.EqualTo(ExceptionHtmlHelper.GetHtml(exception.ToResult())));
        var table = new global::BDTest.StepTable(new[] { "column" }, new[] { new[] { "value" } });
        var argument = table.ToResult();
        Assert.That(StepArgumentHtmlHelper.GetHtml(new[] { table }),
            Is.EqualTo(StepArgumentHtmlHelper.GetHtml(new[] { argument })));
    }

#if !LEGACY_CORE
    [Test]
    public void Empty_snapshot_uses_the_run_clock_and_freezes_the_sampled_time()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(start);
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        clock.Advance(TimeSpan.FromSeconds(3));
        var snapshot = run.GetSnapshot();
        clock.Advance(TimeSpan.FromSeconds(2));
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run));
        Assert.That(snapshot.Timer.TestsStartedAt, Is.EqualTo(start));
        Assert.That(snapshot.Timer.TestsFinishedAt, Is.EqualTo(start.AddSeconds(3)));
        Assert.That(report.TestTimer!.TestsFinishedAt, Is.EqualTo(start.AddSeconds(5)));
    }
#endif
}
