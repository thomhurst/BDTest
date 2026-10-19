using System.Net;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ReportModelTests
{
    private static BDTestRunDescriptor Descriptor() => new()
    {
        Environment = "staging", Tag = "nightly", BranchName = "main",
        CustomProperties = { ["Build <number>"] = "42 & counting" }
    };

    [Test]
    public async Task Json_and_upload_contain_the_same_complete_run_snapshot()
    {
        using var explicitRun = BDTestRun.Start();
        using var otherRun = BDTestRun.Start();
        var run = explicitRun;
        var fixture = new Capture(run);
        var scenario = await fixture.Build().WithScenarioText("selected scenario").BDTestAsync();
        _ = fixture.Build().WithScenarioText("selected pending");
        await new Capture(otherRun).Build().WithScenarioText("other scenario").BDTestAsync();
        _ = new Capture(otherRun).Build().WithScenarioText("other pending");
        var descriptor = Descriptor();
        var json = BDTestJsonHelper.GetTestJsonData(descriptor, run);
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);
        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client, run);

        await Assert.That(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(Required(handler.Payload)))).IsTrue().Because("Formatting may differ, but JSON export and upload must have the same report values.");
        var report = Required(BDTestJsonHelper.Deserialize(json));
        await Assert.That(report.Id).IsEqualTo(run.ReportId);
        await Assert.That(report.Environment).IsEqualTo("staging");
        await Assert.That(report.Tag).IsEqualTo("nightly");
        await Assert.That(report.BranchName).IsEqualTo("main");
        await Assert.That(report.MachineName).IsEqualTo(Environment.MachineName);
        await Assert.That(report.Version).IsEqualTo(BDTestVersionHelper.CurrentVersion);
        await Assert.That(Required(report.Scenarios).Select(item => item.Guid)).IsEquivalentTo(new[] { scenario.Guid }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(report.NotRun).Select(item => item.GetScenarioText())).IsEquivalentTo(new[] { "selected pending" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(scenario.StartTime);
        await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsEqualTo(scenario.EndTime);

        await Assert.That(report.CustomProperties).IsEquivalentTo(new Dictionary<string, string> { ["Build <number>"] = "42 & counting" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);

        await Assert.That(html).Contains("selected scenario").And.Contains("selected pending");

        await Assert.That(html).DoesNotContain("other scenario").And.DoesNotContain("other pending");
        await Assert.That(html).Contains("<dt>Build &lt;number&gt;</dt><dd>42 &amp; counting</dd>");
    }

    [Test]
    public async Task Empty_reports_keep_the_selected_run_start_and_properties()
    {
        using var explicitRun = BDTestRun.Start();
        var run = explicitRun;
        var beforeReport = DateTime.UtcNow;
        var json = BDTestJsonHelper.GetTestJsonData(Descriptor(), run);
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);
        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), Descriptor(), client, run);

        foreach (var payload in new[] { json, Required(handler.Payload) })
        {
            var report = Required(BDTestJsonHelper.Deserialize(payload));
            await Assert.That(report.Id).IsEqualTo(run.ReportId);
            await Assert.That(report.Scenarios).IsEmpty();
            await Assert.That(report.NotRun).IsEmpty();
            await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(run.StartedAt);

            await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsGreaterThanOrEqualTo(beforeReport).And.IsLessThanOrEqualTo(DateTime.UtcNow);
            await Assert.That(Required(report.CustomProperties)["Build <number>"]).IsEqualTo("42 & counting");
        }
        var html = BDTestHtmlHelper.GetTestHtmlReport(Descriptor(), run);
        await Assert.That(html).Contains("No scenarios ran");
        await Assert.That(html).Contains("<dt>Build &lt;number&gt;</dt><dd>42 &amp; counting</dd>");
    }

    [Test]
    public async Task Report_properties_are_copied_at_export_time_even_for_explicit_runs()
    {
        using var run = BDTestRun.Start();
        var descriptor = Descriptor();
        var report = BDTestOutputModelBuilder.Build(run, descriptor);
        descriptor.CustomProperties["Build <number>"] = "next build";

        await Assert.That(Required(report.CustomProperties)["Build <number>"]).IsEqualTo("42 & counting");
        var nextReport = Required(BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(descriptor, run)));
        await Assert.That(Required(nextReport.CustomProperties)["Build <number>"]).IsEqualTo("next build");
    }

    [Test]
    public async Task Reports_reject_null_runs_before_sending_any_request()
    {
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);

        await Assert.That(Required(await Assert.That(() => BDTestJsonHelper.GetTestJsonData(null, null!)).ThrowsExactly<ArgumentNullException>()).ParamName).IsEqualTo("run");
        await Assert.That(Required(await Assert.That(() => BDTestHtmlHelper.GetTestHtmlReport(null, null!)).ThrowsExactly<ArgumentNullException>()).ParamName).IsEqualTo("run");
        var exception = await Assert.That(async () => { await BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri("https://reports.example"), null, client, null!); }).ThrowsExactly<ArgumentNullException>();
        await Assert.That(Required(exception).ParamName).IsEqualTo("run");
        await Assert.That(handler.RequestCount).IsZero();
    }

    [Test]
    public async Task Reports_reject_disposed_runs_before_sending_any_request()
    {
        var run = BDTestRun.Start();
        run.Dispose();
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);

        await Assert.That(() => BDTestJsonHelper.GetTestJsonData(Descriptor(), run)).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(() => BDTestHtmlHelper.GetTestHtmlReport(Descriptor(), run)).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(async () => { await BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri("https://reports.example"), Descriptor(), client, run); }).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(handler.RequestCount).IsZero();
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
