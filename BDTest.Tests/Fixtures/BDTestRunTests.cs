using System.Net;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class BDTestRunTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Rejected_repeat_execution_preserves_the_current_run_and_teardown_output(bool useOutline)
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        Func<Task<Scenario>> execute;
        if (useOutline)
        {
            var outline = fixture.ScenarioOutline("example <Value>", new[] { new { Value = 1 } },
                row => fixture.When(() => Keep(row.Value)).Then(() => Complete()));
            execute = outline.BDTestAsync;
        }
        else
        {
            var builder = fixture.Build();
            execute = builder.BDTestAsync;
        }

        var firstScenario = await execute();
        var secondScenario = await new Capture(second, "same-id").Build().BDTestAsync();
        Assert.Throws<AlreadyExecutedException>(() => execute());
        Assert.That(TestOutputData.Run, Is.SameAs(second));
        Assert.That(TestOutputData.TestId, Is.EqualTo(secondScenario.Guid));
        Assert.That(TestOutputData.FrameworkExecutionId, Is.EqualTo("same-id"));
        Console.WriteLine("second teardown after rejected execution");

        Assert.That(secondScenario.TearDownOutput ?? "", Does.Contain("second teardown after rejected execution"));
        Assert.That(firstScenario.TearDownOutput ?? "", Does.Not.Contain("second teardown after rejected execution"));
    }

    [Test]
    public void Starting_a_run_creates_a_fresh_report_and_empty_stores()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        var builder = fixture.Build();

        Assert.That(first.ReportId, Is.Not.EqualTo(second.ReportId));
        Assert.That(BDTestUtil.GetNotRunScenarios(first), Does.Contain(builder));
        Assert.That(BDTestUtil.GetNotRunScenarios(second), Is.Empty);
        Assert.That(second.Scenarios, Is.Empty);
    }

    [Test]
    public async Task Concurrent_runs_keep_identical_framework_ids_and_output_separate()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        async Task<Scenario> Execute(BDTestRun run, string text)
        {
            var fixture = new Capture(run, "same-id");
            fixture.WriteStartupOutput(text + " setup");
            fixture.ScenarioHtmlWriter.Text(text + " html");
            var steps = new ConcurrentSteps(text, async () =>
            {
                if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            });
            var scenario = await fixture.When(() => steps.Write()).Then(() => Complete()).BDTestAsync();
            fixture.WriteTearDownOutput(text + " teardown");
            return scenario;
        }

        var results = await Task.WhenAll(Task.Run(() => Execute(first, "first")), Task.Run(() => Execute(second, "second")));
        foreach (var (run, text, index) in new[] { (first, "first", 0), (second, "second", 1) })
        {
            var scenario = results[index];
            var other = index == 0 ? "second" : "first";
            Assert.That(run.Scenarios, Is.EqualTo(new[] { scenario }));
            Assert.That(scenario.ReportId, Is.EqualTo(run.ReportId));
            Assert.That(scenario.Steps.All(step => step.ReportId == run.ReportId), Is.True);
            Assert.That(scenario.TestStartupInformation, Does.Contain(text + " setup").And.Not.Contain(other));
            Assert.That(scenario.Output, Does.Contain(text + " before").And.Contain(text + " after").And.Not.Contain(other));
            Assert.That(scenario.TearDownOutput, Does.Contain(text + " teardown").And.Not.Contain(other));
            Assert.That(scenario.CustomHtmlOutputForReport, Does.Contain(text + " html").And.Not.Contain(other));
        }
    }

    [Test]
    public async Task A_delayed_builder_and_outline_retain_their_run()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        var delayed = fixture.Build();
        var other = await new Capture(second, "same-id").Build().BDTestAsync();
        var scenario = await delayed.BDTestAsync();
        var outline = await fixture.ScenarioOutline("example <Value>", new[] { new { Value = 1 }, new { Value = 2 } },
            row => fixture.When(() => Keep(row.Value)).Then(() => Complete())).BDTestAsync();

        Assert.That(first.Scenarios, Is.EquivalentTo(new[] { scenario, outline }));
        Assert.That(second.Scenarios, Is.EqualTo(new[] { other }));
        Assert.That(outline.Examples.All(row => row.ReportId == first.ReportId), Is.True);
        Assert.That(BDTestUtil.GetNotRunScenarios(first), Is.Empty);
    }

    [Test]
    public async Task Clearing_and_disposal_affect_only_the_owned_run()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var firstFixture = new Capture(first, "same-id");
        var secondScenario = await new Capture(second, "same-id").Build().BDTestAsync();
        firstFixture.WriteStartupOutput("discarded setup");
        firstFixture.ScenarioHtmlWriter.Text("discarded html");
        _ = firstFixture.Build();
        BDTestUtil.ClearScenarios(first);
        var scenario = await firstFixture.Build().BDTestAsync();
        Assert.That(scenario.TestStartupInformation, Is.Null.Or.Empty);
        Assert.That(scenario.CustomHtmlOutputForReport, Is.Null.Or.Empty);
        _ = firstFixture.Build();
        first.Dispose();
        first.Dispose();

        Assert.That(first.Scenarios, Is.Empty);
        Assert.That(BDTestUtil.GetNotRunScenarios(first), Is.Empty);
        Assert.That(second.Scenarios, Is.EqualTo(new[] { secondScenario }));
        Assert.Throws<ObjectDisposedException>(() => firstFixture.Build());
    }

    [Test]
    public async Task Context_and_saved_output_writers_use_their_own_run()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var firstFixture = new Capture(first, "same-id");
        var secondFixture = new Capture(second, "same-id");
        firstFixture.Start();
        Console.WriteLine("first startup");
        var firstHtml = firstFixture.ScenarioHtmlWriter;
        var firstScenario = await firstFixture.Build().BDTestAsync();
        secondFixture.Start();
        Console.WriteLine("second startup");
        var secondScenario = await secondFixture.Build().BDTestAsync();
        firstHtml.Text("first saved writer");

        Assert.That(firstFixture.BDTestContext.CurrentScenario, Is.SameAs(firstScenario));
        Assert.That(secondFixture.BDTestContext.CurrentScenario, Is.SameAs(secondScenario));
        Assert.That(firstFixture.BDTestContext.CurrentTestRunnerId, Is.EqualTo(first.ReportId));
        Assert.That(firstScenario.TestStartupInformation, Does.Contain("first startup").And.Not.Contain("second startup"));
        Assert.That(secondScenario.TestStartupInformation, Does.Contain("second startup").And.Not.Contain("first startup"));
        Assert.That(firstScenario.CustomHtmlOutputForReport, Does.Contain("first saved writer"));
        Assert.That(secondScenario.CustomHtmlOutputForReport, Is.Null.Or.Empty);
        firstFixture.Finish();
        secondFixture.Finish();
    }

    [Test]
    public async Task Reports_use_only_the_selected_run()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        var scenario = await fixture.Build().WithScenarioText("first scenario").BDTestAsync();
        _ = fixture.Build().WithScenarioText("first pending");
        await new Capture(second, "same-id").Build().WithScenarioText("second scenario").BDTestAsync();
        var descriptor = new BDTestRunDescriptor { Tag = "explicit run" };
        var json = BDTestJsonHelper.GetTestJsonData(descriptor, first);
        var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, first);
        using var handler = new ReportHandler();
        using var client = new HttpClient(handler);
        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client, first);

        foreach (var payload in new[] { json, handler.Payload })
        {
            var report = JsonConvert.DeserializeObject<BDTestOutputModel>(payload);
            Assert.That(report.Id, Is.EqualTo(first.ReportId));
            Assert.That(report.Tag, Is.EqualTo(descriptor.Tag));
            Assert.That(report.Scenarios.Select(item => item.Guid), Is.EqualTo(new[] { scenario.Guid }));
            Assert.That(report.NotRun.Select(item => item.GetScenarioText()), Is.EqualTo(new[] { "first pending" }));
            Assert.That(report.Scenarios[0].Run, Is.Null, "Run ownership must not be serialized");
        }
        Assert.That(html, Does.Contain("first scenario").And.Contain("first pending").And.Not.Contain("second scenario"));
    }

    [Test]
    public async Task Disposal_clears_pending_output_and_listeners_and_rejects_a_saved_builder()
    {
        using var run = BDTestRun.Start();
        var fixture = new Capture(run, "same-id");
        var builder = fixture.Build();
        run.Store.AppendStartupOutput("pending", "setup", false);
        var listenerCalls = 0;
        run.Store.ListenForScenario("pending", _ => listenerCalls++);
        run.OutputBuffers.Add("pending", new System.Text.StringBuilder("step output"));
        run.Dispose();

        Assert.That(run.OutputBuffers, Is.Empty);
        Assert.That(run.Store.ScenariosByTestFrameworkId, Is.Empty);
        Assert.That(BDTestUtil.GetNotRunScenarios(run), Is.Empty);
        Assert.Throws<ObjectDisposedException>(() => builder.BDTestAsync());
        Assert.Throws<ObjectDisposedException>(() => fixture.ScenarioHtmlWriter.Text("late output"));
        using var fresh = BDTestRun.Start();
        var scenario = await new Capture(fresh, "pending").Build().BDTestAsync();
        Assert.That(scenario.TestStartupInformation, Is.Null.Or.Empty);
        Assert.That(listenerCalls, Is.Zero);
    }

    private static void Complete() { }
    private static void Keep(int value) => GC.KeepAlive(value);

    private sealed class ConcurrentSteps(string text, Func<Task> wait)
    {
        public async Task Write()
        {
            Console.WriteLine(text + " before");
            await wait();
            Console.WriteLine(text + " after");
        }
    }

    public sealed class CaptureContext { }

    private sealed class Capture(BDTestRun run, string id) : AbstractContextBDTestBase<CaptureContext>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => id;
        public BDTest.Test.Steps.Then.Then Build() => When(() => Complete()).Then(() => Complete());
        public void Start() => MarkTestAsStarted();
        public void Finish() => MarkTestAsComplete();
    }

    private sealed class ReportHandler : HttpMessageHandler
    {
        public string Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post) Payload = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://reports.example/report")
            };
        }
    }
}
