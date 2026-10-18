using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class StructuredStepArgumentTests : BDTestBase
{
    public override BDTestRun TestRun { get; } = BDTestRun.Start();
    private int _reads;
    private int _attempt;
    private bool _setBeforeStep;
    private StepTable Table => StepTable.From(["Value"], [++_reads]);

    [SetUp]
    public void BindMetadata() => BeginTest(TestContext.CurrentContext.Test.ID,
        Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);

    [TearDown]
    public void DisposeRun()
    {
        EndTest();
        TestRun.Dispose();
    }

    [Test]
    public void Object_tables_snapshot_public_properties_and_null_cells()
    {
        var rows = new[] { new { Name = "<Alice>", Age = (int?)null }, new { Name = "Bob", Age = (int?)42 } };
        var table = StepTable.From(rows);
        Assert.That(table.Headers, Is.EqualTo(new[] { "Age", "Name" }));
        Assert.That(table.Rows[0], Is.EqualTo(new[] { "", "<Alice>" }));
        Assert.That(table.Rows[1], Is.EqualTo(new[] { "42", "Bob" }));

        string[] headers = ["Name"];
        object?[][] cells = [["before"], [null]];
        var explicitTable = StepTable.From(headers, cells);
        headers[0] = "changed";
        cells[0][0] = "after";
        Assert.That(explicitTable.Headers, Is.EqualTo(new[] { "Name" }));
        Assert.That(explicitTable.Rows.Select(row => row[0]), Is.EqualTo(new[] { "before", "" }));
        Assert.Throws<ArgumentException>(() => StepTable.From(["one"], [1, 2]));
    }

    [Test]
    public async Task Generated_arguments_are_lazy()
    {
        var builder = When(() => Accept(Table)).Then(NoOp);
        Assert.That(_reads, Is.Zero);
        Assert.That(builder.ExistingSteps[0], Is.TypeOf<StepDefinition>());
        var result = await builder;
        Assert.That(_reads, Is.EqualTo(2), "One report read and the actual delegate invocation.");
        Assert.That(Required(result.Steps)[0].StepText, Does.Contain("(table: 1 rows)"));
        Assert.That(((StepTable)result.Steps[0].Arguments.Single()).Rows[0][0], Is.EqualTo("1"));
    }

    [Test]
    public async Task Attachments_wait_for_hooks_and_reports_do_not_read_pending_arguments()
    {
        _setBeforeStep = true;
        var builder = Given(NoOp).WithTable(() => Table).When(NoOp).Then(NoOp);
        _ = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), TestRun);
        _ = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), TestRun);
        Assert.That(_reads, Is.Zero);
        var result = await builder;
        Assert.That(((StepTable)Required(result.Steps)[0].Arguments[0]).Rows[0][0], Is.EqualTo("11"));
    }

    public override Task OnBeforeStep(Step step)
    {
        if (_setBeforeStep)
            _reads = 10;
        return Task.CompletedTask;
    }

    [Test]
    public async Task Unreached_attachments_are_not_read_and_reader_failures_are_diagnostics()
    {
        // A skip rule is scoped to this explicit run, so other fixtures remain unaffected.
        var options = new BDTestOptions();
        options.SkipTestRules.Add(_ => true);
        using var skippedRun = BDTestRun.Start(options);
        var fixture = new SkippedCapture(skippedRun);
        var skippedResult = await fixture.Build(() => Table);
        Assert.That(skippedResult.Status, Is.EqualTo(Status.Skipped));
        Assert.That(_reads, Is.Zero);
        Assert.That(Required(skippedResult.Steps).SelectMany(step => step.Arguments), Is.Empty);
        var result = await When(NoOp).WithTable(() => throw new InvalidOperationException("bad table"))
            .WithDocString(new StepDocString("retained")).Then(NoOp);
        Assert.That(result.Status, Is.EqualTo(Status.Passed));
        Assert.That(Required(Required(result.Steps)[0].Diagnostics).Single(), Does.Contain("bad table"));
        Assert.That(result.Steps[0].Arguments.Single(), Is.TypeOf<StepDocString>());
    }

    [Test]
    public async Task Explicit_readers_capture_tables_and_doc_strings_with_short_placeholders()
    {
        var table = StepTable.From(["name"], ["Alice"]);
        var doc = new StepDocString("{\n  \"name\": \"Alice\"\n}", "application/json");
        var result = await Given(() => Accept(table)).WithArgs(() => table)
            .When(() => Accept(doc)).WithArgs(() => doc).Then(NoOp);
        Assert.That(Required(result.Steps)[0].StepText, Does.Contain("(table: 1 rows)"));
        Assert.That(result.Steps[1].StepText, Does.Contain("(application/json)"));
        Assert.That(result.Steps.SelectMany(step => step.Arguments), Is.EqualTo(new StepArgument[] { table, doc }));
    }

    [Test, BDTestRetry(1)]
    public async Task Attachments_refresh_per_retry_and_keep_sibling_definitions_independent()
    {
        var prefix = Given(NoOp).WithTable(() => Table).WithStepText(() => "custom text");
        var first = prefix.When(NoOp).Then(() => FailOnce());
        var sibling = prefix.When(NoOp).Then(NoOp).WithDocString(new StepDocString("sibling"));
        Assert.That(_reads, Is.Zero);
        var result = await first;
        Assert.That(result.RetryCount, Is.EqualTo(1));
        Assert.That(Required(result.Steps)[0].Arguments, Has.Count.EqualTo(1));
        Assert.That(((StepTable)result.Steps[0].Arguments[0]).Rows[0][0], Is.EqualTo("2"));
        var other = await sibling;
        Assert.That(((StepTable)Required(other.Steps)[0].Arguments[0]).Rows[0][0], Is.EqualTo("3"));
        Assert.That(other.Steps[2].Arguments, Has.Count.EqualTo(1));
        Assert.That(result.Steps[2].Arguments, Is.Empty);
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(result));
        Assert.That(((StepTable)Required(restored.Steps)[0].Arguments[0]).Rows[0][0], Is.EqualTo("2"));
    }

    [Test]
    public async Task Outline_rows_keep_their_own_attachments()
    {
        var outline = await ScenarioOutline("payloads", new[] { "first", "second" },
            row => Given(NoOp).WithDocString(() => new StepDocString(row))
                .When(NoOp).Then(NoOp));
        Assert.That(Required(outline.Examples).Select(row => ((StepDocString)Required(row.Steps)[0].Arguments.Single()).Content),
            Is.EqualTo(new[] { "first", "second" }));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        Assert.That(Required(restored.Examples).Select(row => ((StepDocString)Required(row.Steps)[0].Arguments.Single()).Content),
            Is.EqualTo(new[] { "first", "second" }));
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored);
        Assert.That(html, Does.Contain("first</code></pre>").And.Contain("second</code></pre>"));
    }

    [Test]
    public async Task Json_upload_and_both_renderers_preserve_and_encode_large_arguments()
    {
        const string unsafeText = "</pre><script>alert('x')</script> & \"";
        var table = StepTable.From([unsafeText], Enumerable.Range(0, 1000)
            .Select(index => new object[] { index == 999 ? unsafeText : index }).ToArray());
        var doc = new StepDocString(unsafeText + "\nsecond line", unsafeText);
        await Given(NoOp).WithTable(table).WithDocString(doc).When(NoOp).Then(NoOp);
        using var handler = new UploadHandler();
        using var client = new HttpClient(handler);
        await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), new BDTestRunDescriptor(), client, TestRun);
        var restored = Required(BDTestJsonHelper.Deserialize(Required(handler.Json)));
        var step = Required(Required(restored.Scenarios).Single().Steps)[0];
        Assert.That(((StepTable)step.Arguments[0]).Rows, Has.Count.EqualTo(1000));
        Assert.That(((StepDocString)step.Arguments[1]).Content, Is.EqualTo(doc.Content));
        Assert.That(((StepDocString)step.Arguments[1]).ContentType, Is.EqualTo(unsafeText));
        var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored.Scenarios.Single());
        foreach (var html in new[] { razor, BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), TestRun) })
        {
            Assert.That(html, Does.Contain("<table").And.Contain("<pre").And.Contain("<details"));
            Assert.That(html, Does.Contain("Show all 1000 rows").And.Contain("&lt;script&gt;").And.Contain("second line"));
            Assert.That(html, Does.Not.Contain(unsafeText));
            var argument = Regex.Match(html, "<div class=\"step-argument\".*?</details>", RegexOptions.Singleline).Value;
            Assert.That(Regex.Matches(argument.Split("<details>")[0], "<tr>").Count, Is.EqualTo(21), "Header plus 20 preview rows.");
            Assert.That(Regex.Matches(argument, "<td>").Count, Is.EqualTo(1000), "All cells remain available when expanded.");
        }
        var legacyStep = ReportJson.DeserializeObject<Step>("{\"StepText\":\"Given legacy\"}");
        Assert.That(legacyStep.Arguments, Is.Empty);
        Assert.That(ReportJson.DeserializeObject<Step>("{\"Arguments\":null}").Arguments, Is.Empty);
    }

    private static void Accept(object value) { }
    private static void NoOp() { }
    private void FailOnce()
    {
        if (_attempt++ == 0) throw new InvalidOperationException("retry");
    }

    private sealed class SkippedCapture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public RunnableStepBuilder Build(Func<StepTable> table) => Given(NoOp).WithTable(table).When(NoOp).Then(NoOp);
    }

    private sealed class UploadHandler : HttpMessageHandler
    {
        public string? Json { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post) Json = await Required(request.Content).ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("https://reports.example/report") };
        }
    }
}
