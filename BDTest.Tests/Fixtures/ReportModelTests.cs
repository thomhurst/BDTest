using System.Net;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportModelTests
{
    private Dictionary<string, string>? _customProperties;

    [SetUp]
    public void SetUp()
    {
        TestResetHelper.ResetData();
        _customProperties = new Dictionary<string, string>(LegacySettings.CustomProperties);
        LegacySettings.CustomProperties.Clear();
        LegacySettings.CustomProperties.Add("Build <number>", "42 & counting");
    }

    [TearDown]
    public void TearDown()
    {
        LegacySettings.CustomProperties.Clear();
        foreach (var property in Required(_customProperties))
            LegacySettings.CustomProperties.Add(property.Key, property.Value);
        TestResetHelper.ResetData();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Json_and_upload_contain_the_same_complete_run_snapshot(bool useDefaultRun)
    {
        using var explicitRun = BDTestRun.Start();
        using var otherRun = BDTestRun.Start();
        var run = useDefaultRun ? BDTestRun.Default : explicitRun;
        var fixture = new Capture(run);
        var scenario = await fixture.Build().WithScenarioText("selected scenario").BDTestAsync();
        _ = fixture.Build().WithScenarioText("selected pending");
        await new Capture(otherRun).Build().WithScenarioText("other scenario").BDTestAsync();
        _ = new Capture(otherRun).Build().WithScenarioText("other pending");
        var descriptor = new BDTestRunDescriptor { Environment = "staging", Tag = "nightly", BranchName = "main" };
        var json = useDefaultRun
            ? BDTestJsonHelper.GetTestJsonData(descriptor)
            : BDTestJsonHelper.GetTestJsonData(descriptor, run);
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);
        if (useDefaultRun)
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client);
        else
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client, run);

        Assert.That(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(Required(handler.Payload))), Is.True,
            "Formatting may differ, but JSON export and upload must have the same report values.");
        var report = BDTestJsonHelper.Deserialize(json);
        Assert.That(report.Id, Is.EqualTo(run.ReportId));
        Assert.That(report.Environment, Is.EqualTo("staging"));
        Assert.That(report.Tag, Is.EqualTo("nightly"));
        Assert.That(report.BranchName, Is.EqualTo("main"));
        Assert.That(report.MachineName, Is.EqualTo(Environment.MachineName));
        Assert.That(report.Version, Is.EqualTo(BDTestVersionHelper.CurrentVersion));
        Assert.That(report.Scenarios.Select(item => item.Guid), Is.EqualTo(new[] { scenario.Guid }));
        Assert.That(report.NotRun.Select(item => item.GetScenarioText()), Is.EqualTo(new[] { "selected pending" }));
        Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(scenario.StartTime));
        Assert.That(report.TestTimer.TestsFinishedAt, Is.EqualTo(scenario.EndTime));
        Assert.That(report.CustomProperties, Is.EquivalentTo(new Dictionary<string, string> { ["Build <number>"] = "42 & counting" }));

        var html = useDefaultRun
            ? BDTestHtmlHelper.GetTestHtmlReport(descriptor)
            : BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);
        Assert.That(html, Does.Contain("selected scenario").And.Contain("selected pending"));
        Assert.That(html, Does.Not.Contain("other scenario").And.Not.Contain("other pending"));
        Assert.That(html, Does.Contain("<dt>Build &lt;number&gt;</dt><dd>42 &amp; counting</dd>"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Empty_reports_keep_the_selected_run_start_and_properties(bool useDefaultRun)
    {
        using var explicitRun = BDTestRun.Start();
        var run = useDefaultRun ? BDTestRun.Default : explicitRun;
        var beforeReport = DateTime.UtcNow;
        var json = useDefaultRun ? BDTestJsonHelper.GetTestJsonData() : BDTestJsonHelper.GetTestJsonData(null, run);
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);
        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), null, client, run);

        foreach (var payload in new[] { json, Required(handler.Payload) })
        {
            var report = BDTestJsonHelper.Deserialize(payload);
            Assert.That(report.Id, Is.EqualTo(run.ReportId));
            Assert.That(report.Scenarios, Is.Empty);
            Assert.That(report.NotRun, Is.Empty);
            Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(run.StartedAt));
            Assert.That(report.TestTimer.TestsFinishedAt, Is.InRange(beforeReport, DateTime.UtcNow));
            Assert.That(report.CustomProperties["Build <number>"], Is.EqualTo("42 & counting"));
        }
        var html = useDefaultRun ? BDTestHtmlHelper.GetTestHtmlReport() : BDTestHtmlHelper.GetTestHtmlReport(null, run);
        Assert.That(html, Does.Contain("No scenarios ran"));
        Assert.That(html, Does.Contain("<dt>Build &lt;number&gt;</dt><dd>42 &amp; counting</dd>"));
    }

    [Test]
    public void Report_properties_are_copied_at_export_time_even_for_explicit_runs()
    {
        using var run = BDTestRun.Start();
        var report = BDTestOutputModelBuilder.Build(run, null);
        LegacySettings.CustomProperties["Build <number>"] = "next build";

        Assert.That(report.CustomProperties["Build <number>"], Is.EqualTo("42 & counting"));
        var nextReport = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run));
        Assert.That(nextReport.CustomProperties["Build <number>"], Is.EqualTo("next build"));
    }

    [Test]
    public async Task Reports_reject_null_runs_before_sending_any_request()
    {
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);

        Assert.That(Required(Assert.Throws<ArgumentNullException>(() => BDTestJsonHelper.GetTestJsonData(null, null))).ParamName,
            Is.EqualTo("run"));
        Assert.That(Required(Assert.Throws<ArgumentNullException>(() => BDTestHtmlHelper.GetTestHtmlReport(null, null))).ParamName,
            Is.EqualTo("run"));
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri("https://reports.example"), null, client, null));
        Assert.That(Required(exception).ParamName, Is.EqualTo("run"));
        Assert.That(handler.RequestCount, Is.Zero);
    }

    [Test]
    public async Task Reports_reject_disposed_runs_before_sending_any_request()
    {
        var run = BDTestRun.Start();
        run.Dispose();
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);

        Assert.Throws<ObjectDisposedException>(() => BDTestJsonHelper.GetTestJsonData(null, run));
        Assert.Throws<ObjectDisposedException>(() => BDTestHtmlHelper.GetTestHtmlReport(null, run));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri("https://reports.example"), null, client, run));
        Assert.That(handler.RequestCount, Is.Zero);
    }

    private static void Complete() { }

    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public BDTest.Test.Steps.Then.Then Build() => When(() => Complete()).Then(() => Complete());
    }

    private sealed class ReportHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.Method == HttpMethod.Post)
                Payload = await Required(request.Content).ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("https://reports.example/report") };
        }
    }
}
