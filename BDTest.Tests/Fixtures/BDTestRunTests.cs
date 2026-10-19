using System.Net;
using System.Text.Json;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Output;
using BDTest;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class BDTestRunTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Rejected_repeat_execution_preserves_the_current_run_and_teardown_output(bool useOutline)
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        Func<Task<BDTest.Contracts.ScenarioResult>> execute;
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
        // Invoke in this flow: an async assertion delegate would restore AsyncLocal output targets.
        Exception? rejected = null;
        try
        {
            _ = execute();
        }
        catch (Exception exception)
        {
            rejected = exception;
        }
        await Assert.That(rejected?.GetType()).IsEqualTo(typeof(AlreadyExecutedException));
        await Assert.That(TestOutputData.Run).IsSameReferenceAs(second);
        await Assert.That(TestOutputData.TestId).IsEqualTo(secondScenario.Guid);
        await Assert.That(TestOutputData.FrameworkExecutionId).IsEqualTo("same-id");
        Console.WriteLine("second teardown after rejected execution");

        await Assert.That(second.Scenarios.Single().TearDownOutput ?? "").Contains("second teardown after rejected execution");
        await Assert.That(firstScenario.TearDownOutput ?? "").DoesNotContain("second teardown after rejected execution");
    }

    [Test]
    public async Task Starting_a_run_creates_a_fresh_report_and_empty_stores()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new Capture(first, "same-id");
        var builder = fixture.Build();

        await Assert.That(first.ReportId).IsNotEqualTo(second.ReportId);
        await Assert.That(BDTestUtil.GetNotRunScenarios(first).Select(item => item.ScenarioId)).Contains(Required(builder.Guid));
        await Assert.That(BDTestUtil.GetNotRunScenarios(second)).IsEmpty();
        await Assert.That(second.Scenarios).IsEmpty();
    }

    [Test]
    public async Task Concurrent_runs_keep_identical_framework_ids_and_output_separate()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        async Task<BDTest.Contracts.ScenarioResult> Execute(BDTestRun run, string text)
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
            var scenario = run.Scenarios.Single(s => s.Guid == results[index].Guid);
            var other = index == 0 ? "second" : "first";
            await Assert.That(run.Scenarios.Select(s => s.Guid)).IsEquivalentTo(new[] { scenario.Guid }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(scenario.ReportId).IsEqualTo(run.ReportId);
            await Assert.That(Required(scenario.Steps).All(step => step.ReportId == run.ReportId)).IsTrue();

            await Assert.That(scenario.TestStartupInformation).Contains(text + " setup").And.DoesNotContain(other);

            await Assert.That(scenario.Output).Contains(text + " before").And.Contains(text + " after").And.DoesNotContain(other);

            await Assert.That(scenario.TearDownOutput).Contains(text + " teardown").And.DoesNotContain(other);

            await Assert.That(scenario.CustomHtmlOutputForReport).Contains(text + " html").And.DoesNotContain(other);
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


        await Assert.That(first.Scenarios).IsEquivalentTo(new[] { scenario, outline }, EqualityComparer<BDTest.Contracts.ScenarioResult>.Create((left, right) => new KellermanSoftware.CompareNetObjects.CompareLogic().Compare(left, right).AreEqual, _ => 0), global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        await Assert.That(second.Scenarios.Select(s => s.Guid)).IsEquivalentTo(new[] { other.Guid }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(outline.Examples).All(row => row.ReportId == first.ReportId)).IsTrue();
        await Assert.That(BDTestUtil.GetNotRunScenarios(first)).IsEmpty();
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

        await Assert.That(scenario.TestStartupInformation).IsNullOrEmpty();

        await Assert.That(scenario.CustomHtmlOutputForReport).IsNullOrEmpty();
        _ = firstFixture.Build();
        first.Dispose();
        first.Dispose();

        await Assert.That(first.Scenarios).IsEmpty();
        await Assert.That(BDTestUtil.GetNotRunScenarios(first)).IsEmpty();
        await Assert.That(second.Scenarios).IsEquivalentTo(new[] { secondScenario }, EqualityComparer<BDTest.Contracts.ScenarioResult>.Create((left, right) => new KellermanSoftware.CompareNetObjects.CompareLogic().Compare(left, right).AreEqual, _ => 0), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(() => firstFixture.Build()).ThrowsExactly<ObjectDisposedException>();
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

        await Assert.That(firstFixture.BDTestContext.CurrentScenario?.Guid).IsEqualTo(firstScenario.Guid);
        await Assert.That(secondFixture.BDTestContext.CurrentScenario?.Guid).IsEqualTo(secondScenario.Guid);
        await Assert.That(firstFixture.BDTestContext.ReportId).IsEqualTo(first.ReportId);

        await Assert.That(firstScenario.TestStartupInformation).Contains("first startup").And.DoesNotContain("second startup");

        await Assert.That(secondScenario.TestStartupInformation).Contains("second startup").And.DoesNotContain("first startup");
        await Assert.That(first.Scenarios.Single().CustomHtmlOutputForReport).Contains("first saved writer");

        await Assert.That(secondScenario.CustomHtmlOutputForReport).IsNullOrEmpty();
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
            var report = Required(BDTestJsonHelper.DeserializeReport(payload));
            await Assert.That(report.Id).IsEqualTo(first.ReportId);
            await Assert.That(report.Tag).IsEqualTo(descriptor.Tag);
            await Assert.That(Required(report.Scenarios).Select(item => item.Guid)).IsEquivalentTo(new[] { scenario.Guid }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(Required(report.NotRun).Select(item => item.ScenarioText)).IsEquivalentTo(new[] { "first pending" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(typeof(BDTest.Contracts.ScenarioResult).GetProperty("Run")).IsNull();
            await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(scenario.StartTime);
            await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsEqualTo(scenario.EndTime);
            using var document = JsonDocument.Parse(payload);
            await Assert.That(document.RootElement.GetProperty("scenarios")[0].TryGetProperty("run", out _)).IsFalse();
            await Assert.That(document.RootElement.GetProperty("notRun")[0].TryGetProperty("run", out _)).IsFalse();
        }

        await Assert.That(html).Contains("first scenario").And.Contains("first pending").And.DoesNotContain("second scenario");
    }

    [Test]
    public async Task Empty_report_uses_the_selected_run_start_time()
    {
        using var run = BDTestRun.Start();
        var beforeReport = DateTime.UtcNow;

        var report = Required(BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(null, run)));

        await Assert.That(report.Id).IsEqualTo(run.ReportId);
        await Assert.That(report.Scenarios).IsEmpty();
        await Assert.That(Required(report.TestTimer).TestsStartedAt).IsEqualTo(new DateTimeOffset(run.StartedAt));

        await Assert.That(Required(report.TestTimer).TestsFinishedAt).IsGreaterThanOrEqualTo(new DateTimeOffset(beforeReport)).And.IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "Invoking the disposed builder must throw before returning a task.")]
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

        await Assert.That(run.OutputBuffers).IsEmpty();
        await Assert.That(run.Store.ScenariosByTestFrameworkId).IsEmpty();
        await Assert.That(BDTestUtil.GetNotRunScenarios(run)).IsEmpty();
        await Assert.That(async () => { await builder.BDTestAsync(); }).ThrowsExactly<ObjectDisposedException>();
        await Assert.That(() => fixture.ScenarioHtmlWriter.Text("late output")).ThrowsExactly<ObjectDisposedException>();
        using var fresh = BDTestRun.Start();
        var scenario = await new Capture(fresh, "pending").Build().BDTestAsync();

        await Assert.That(scenario.TestStartupInformation).IsNullOrEmpty();
        await Assert.That(listenerCalls).IsZero();
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
        public BDTest.Then Build() => When(() => Complete()).Then(() => Complete());
        public void Start() => BeginTest(id);
        public void Finish() => EndTest();
    }

    private sealed class ReportHandler : HttpMessageHandler
    {
        public string Payload { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post) Payload = await Required(request.Content).ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://reports.example/report")
            };
        }
    }
}
