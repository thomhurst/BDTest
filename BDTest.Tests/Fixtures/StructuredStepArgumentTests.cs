using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class StructuredStepArgumentTests : BDTestBase
{
    public override BDTestRun TestRun { get; } = BDTestRun.Start();
    private int _reads;
    private int _attempt;
    private bool _setBeforeStep;
    private StepTable Table => StepTable.From(["Value"], [++_reads]);

    [Before(HookType.Test)]
    public void BindMetadata() => BeginTest(Required(TestContext.Current).Id,
        Required(TestContext.Current).Metadata.TestDetails.MethodMetadata.GetReflectionInfo());

    [After(HookType.Test)]
    public void DisposeRun()
    {
        EndTest();
        TestRun.Dispose();
    }

    [Test]
    public async Task Object_tables_snapshot_public_properties_and_null_cells()
    {
        var rows = new[] { new { Name = "<Alice>", Age = (int?)null }, new { Name = "Bob", Age = (int?)42 } };
        var table = StepTable.From(rows);
        await Assert.That(table.Headers).IsEquivalentTo(new[] { "Age", "Name" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(table.Rows[0]).IsEquivalentTo(new[] { "", "<Alice>" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(table.Rows[1]).IsEquivalentTo(new[] { "42", "Bob" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        string[] headers = ["Name"];
        object?[][] cells = [["before"], [null]];
        var explicitTable = StepTable.From(headers, cells);
        headers[0] = "changed";
        cells[0][0] = "after";
        await Assert.That(explicitTable.Headers).IsEquivalentTo(new[] { "Name" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(explicitTable.Rows.Select(row => row[0])).IsEquivalentTo(new[] { "before", "" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(() => StepTable.From(["one"], [1, 2])).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Generated_arguments_are_lazy()
    {
        var builder = When(() => Accept(Table)).Then(NoOp);
        await Assert.That(_reads).IsZero();
        await Assert.That(builder.ExistingSteps[0]).IsTypeOf<StepDefinition>();
        var result = await builder;
        await Assert.That(_reads).IsEqualTo(2).Because("One report read and the actual delegate invocation.");
        await Assert.That(Required(result.Steps)[0].StepText).Contains("(table: 1 rows)");
        await Assert.That(((StepTable)result.Steps[0].Arguments.Single()).Rows[0][0]).IsEqualTo("1");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    public async Task Attachments_wait_for_hooks_and_reports_do_not_read_pending_arguments()
    {
        _setBeforeStep = true;
        var builder = Given(NoOp).WithTable(() => Table).When(NoOp).Then(NoOp);
        _ = BDTestJsonHelper.GetTestJsonData(new BDTestRunDescriptor(), TestRun);
        _ = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), TestRun);
        await Assert.That(_reads).IsZero();
        var result = await builder;
        await Assert.That(((StepTable)Required(result.Steps)[0].Arguments[0]).Rows[0][0]).IsEqualTo("11");
    }

    public override Task OnBeforeStep(BDTest.Contracts.StepResult step)
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
        await Assert.That(skippedResult.Status).IsEqualTo(Status.Skipped);
        await Assert.That(_reads).IsZero();
        await Assert.That(Required(skippedResult.Steps).SelectMany(step => step.Arguments)).IsEmpty();
        var result = await When(NoOp).WithTable(() => throw new InvalidOperationException("bad table"))
            .WithDocString(new StepDocString("retained")).Then(NoOp);
        await Assert.That(result.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(Required(result.Steps)[0].Diagnostics).Single()).Contains("bad table");
        await Assert.That(result.Steps[0].Arguments.Single()).IsTypeOf<StepDocString>();
    }

    [Test]
    public async Task Explicit_readers_capture_tables_and_doc_strings_with_short_placeholders()
    {
        var table = StepTable.From(["name"], ["Alice"]);
        var doc = new StepDocString("{\n  \"name\": \"Alice\"\n}", "application/json");
        var result = await Given(() => Accept(table)).WithArgs(() => table)
            .When(() => Accept(doc)).WithArgs(() => doc).Then(NoOp);
        await Assert.That(Required(result.Steps)[0].StepText).Contains("(table: 1 rows)");
        await Assert.That(result.Steps[1].StepText).Contains("(application/json)");
        await Assert.That(result.Steps.SelectMany(step => step.Arguments)).IsEquivalentTo(new StepArgument[] { table, doc }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test, BDTestRetry(1)]
    public async Task Attachments_refresh_per_retry_and_keep_sibling_definitions_independent()
    {
        var prefix = Given(NoOp).WithTable(() => Table).WithStepText(() => "custom text");
        var first = prefix.When(NoOp).Then(() => FailOnce());
        var sibling = prefix.When(NoOp).Then(NoOp).WithDocString(new StepDocString("sibling"));
        await Assert.That(_reads).IsZero();
        var result = await first;
        await Assert.That(result.RetryCount).IsEqualTo(1);
        await Assert.That(Required(result.Steps)[0].Arguments).Count().IsEqualTo(1);
        await Assert.That(((StepTable)result.Steps[0].Arguments[0]).Rows[0][0]).IsEqualTo("2");
        var other = await sibling;
        await Assert.That(((StepTable)Required(other.Steps)[0].Arguments[0]).Rows[0][0]).IsEqualTo("3");
        await Assert.That(other.Steps[2].Arguments).Count().IsEqualTo(1);
        await Assert.That(result.Steps[2].Arguments).IsEmpty();
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(result));
        await Assert.That(((StepTable)Required(restored.Steps)[0].Arguments[0]).Rows[0][0]).IsEqualTo("2");
    }

    [Test]
    public async Task Outline_rows_keep_their_own_attachments()
    {
        var outline = await ScenarioOutline("payloads", new[] { "first", "second" },
            row => Given(NoOp).WithDocString(() => new StepDocString(row))
                .When(NoOp).Then(NoOp));
        await Assert.That(Required(outline.Examples).Select(row => ((StepDocString)Required(row.Steps)[0].Arguments.Single()).Content)).IsEquivalentTo(new[] { "first", "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        await Assert.That(Required(restored.Examples).Select(row => ((StepDocString)Required(row.Steps)[0].Arguments.Single()).Content)).IsEquivalentTo(new[] { "first", "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var html = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored);

        await Assert.That(html).Contains("first</code></pre>").And.Contains("second</code></pre>");
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
        var restored = Required(BDTestJsonHelper.DeserializeReport(Required(handler.Json)));
        var step = Required(Required(restored.Scenarios).Single().Steps)[0];
        await Assert.That(((StepTable)step.Arguments[0]).Rows).Count().IsEqualTo(1000);
        await Assert.That(((StepDocString)step.Arguments[1]).Content).IsEqualTo(doc.Content);
        await Assert.That(((StepDocString)step.Arguments[1]).ContentType).IsEqualTo(unsafeText);
        var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored.Scenarios.Single());
        foreach (var html in new[] { razor, BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), TestRun) })
        {

            await Assert.That(html).Contains("<table").And.Contains("<pre").And.Contains("<details");

            await Assert.That(html).Contains("Show all 1000 rows").And.Contains("&lt;script&gt;").And.Contains("second line");
            await Assert.That(html).DoesNotContain(unsafeText);
            var argument = Regex.Match(html, "<div class=\"step-argument\".*?</details>", RegexOptions.Singleline).Value;
            await Assert.That(Regex.Matches(argument.Split("<details>")[0], "<tr>").Count).IsEqualTo(21).Because("Header plus 20 preview rows.");
            await Assert.That(Regex.Matches(argument, "<td>").Count).IsEqualTo(1000).Because("All cells remain available when expanded.");
        }
        var legacyStep = ReportJson.DeserializeObject<Step>("{\"StepText\":\"Given legacy\"}");
        await Assert.That(legacyStep.Arguments).IsEmpty();
        await Assert.That(ReportJson.DeserializeObject<Step>("{\"Arguments\":null}").Arguments).IsEmpty();
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
