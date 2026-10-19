using System.Text;
using System.Text.Json;
using BDTest.Contracts;
using BDTest.Razor.Reports.Serialization;

namespace BDTest.Tests.Fixtures;

public class HostedReportJsonTests
{
    [Test]
    public async Task Current_and_legacy_reports_preserve_the_same_rich_contract()
    {
        var start = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromTicks(12345678);
        var attachment = new AttachmentInfo("attachment", "capture.txt", "text/plain", 42, 2, "attachments/capture.txt");
        var exception = new ExceptionInfo("System.InvalidOperationException", "failed", "stack", "exception text", "InvalidOperationException",
            new Dictionary<string, string> { ["key"] = "value" }, -1, "source",
            [new ExceptionInfo("System.Exception", "inner", truncationReason: "DepthLimit")], true);
        var step = new StepResult(StepType.ButWhen, "But action", Status.Failed, "step", "report", "scenario", true,
            exception, "step output", start, start.Add(duration), duration, TimeSpan.FromSeconds(2), true,
            ["diagnostic"], [new Contracts.StepTable(["header"], [new[] { "cell" }]), new Contracts.StepDocString("body", "text/plain")],
            [attachment], new StepSkipReason("Rule", "My.SkipAttribute", 3));
        var example = new ScenarioResult("example", "report", "story", "example", Status.Passed,
            exampleValues: new Dictionary<string, string> { ["input"] = "row" }, outlineId: "scenario");
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Failed, [step],
            [new ScenarioAttemptResult(2, Status.Failed, exception, start, start.Add(duration), duration, [step])],
            new Dictionary<string, string> { ["input"] = "value" }, [example], ["tag"], exception,
            "framework", "test.cs", "startup", "teardown", "<p>html</p>", "output", start, start.Add(duration), duration,
            TimeSpan.FromSeconds(3), 1, true, attachments: [attachment], customTestInformation: ["information"]);
        var definition = new ScenarioDefinitionSummary("unrun", "report", "unrun story", "unrun scenario", "test", "member", "file.cs",
            ["parameter"], ["pending"], ["pending information"], TimeSpan.FromSeconds(4),
            [new StepDefinitionSummary(StepType.AndThen, "And pending", TimeSpan.FromSeconds(1))]);
        var expected = new BDTestReport(1, "report", "arbitrary producer text", [scenario], [definition],
            "environment", "report tag", "branch", "machine", new ReportTimer(start, start.AddSeconds(5)),
            new Dictionary<string, string> { ["property"] = "value" }, "hash");
        var currentJson = BDTestReportJson.Serialize(expected);
        var current = Required(HostedReportJson.Deserialize(currentJson));
        await Assert.That(BDTestReportJson.Serialize(current)).IsEqualTo(currentJson);

        var legacy = Required(HostedReportJson.Deserialize(RichLegacyJson));
        await Assert.That(BDTestReportJson.Serialize(legacy)).IsEqualTo(currentJson);
        // The historical execution exception is represented as data, never reconstructed as a CLR exception.
        await Assert.That(legacy.Scenarios[0].Exception!.InnerExceptions[0].TruncationReason).IsEqualTo("DepthLimit");
    }

    [Test]
    public async Task Historical_Newtonsoft_fixture_retains_statuses_text_and_timing()
    {
        var report = Required(HostedReportJson.Deserialize(HistoricalReportFixture.ReadJson()));
        await Assert.That(report.Id).IsEqualTo("1113e3bf269c489187245c4339dfd946");
        await Assert.That(report.ProducerVersion).IsEqualTo("1.0.0.0");
        await Assert.That(report.Scenarios).Count().IsEqualTo(13);
        await Assert.That(report.Scenarios.Count(item => item.Status == Status.Passed)).IsEqualTo(3);
        await Assert.That(report.Scenarios.Count(item => item.Status == Status.Failed)).IsEqualTo(6);
        await Assert.That(report.Scenarios.Count(item => item.Status == Status.Inconclusive)).IsEqualTo(4);
        await Assert.That(report.Scenarios[0].ScenarioText).IsEqualTo("An inconclusive when test");
        await Assert.That(report.Scenarios[0].Steps[0].TimeTaken.Ticks).IsEqualTo(3728);
        await Assert.That(report.Scenarios[0].Steps[1].StartTime).IsEqualTo(default(DateTimeOffset));
        await Assert.That(report.TestTimer!.TestsStartedAt).IsEqualTo(new DateTimeOffset(2021, 9, 24, 11, 29, 33, TimeSpan.Zero).AddTicks(7505104));
        await Assert.That(report.Scenarios.All(item => item.Attempts.Count == 0 && item.Attachments.Count == 0)).IsTrue();
    }

    [Test]
    [Arguments("0", Status.Passed)]
    [Arguments("1", Status.Failed)]
    [Arguments("2", Status.Inconclusive)]
    [Arguments("3", Status.NotImplemented)]
    [Arguments("4", Status.Skipped)]
    [Arguments("\"Processing\"", Status.Pending)]
    public async Task Legacy_statuses_are_mapped_in_every_location(string value, Status expected)
    {
        var report = Required(HostedReportJson.Deserialize($$"""
            {"Scenarios":[{"Status":{{value}},"Steps":[{"Status":{{value}}}],"Attempts":[{"Status":{{value}}}]}]}
            """));
        var scenario = report.Scenarios.Single();
        await Assert.That(scenario.Status).IsEqualTo(expected);
        await Assert.That(scenario.Steps.Single().Status).IsEqualTo(expected);
        await Assert.That(scenario.Attempts.Single().Status).IsEqualTo(expected);
    }

    [Test]
    public async Task Legacy_references_wrappers_and_metadata_named_dictionary_entries_are_preserved()
    {
        var report = Required(HostedReportJson.Deserialize("""
            {"$id":"report","id":"legacy","CustomProperties":{"$id":"properties","$ref":"value","$values":"other"},
             "Scenarios":{"$id":"list","$values":[{"$id":"scenario","Steps":[
               {"$id":"step","StepText":"Then fail","Exception":{"TypeName":"Error","Message":"failed"}},{"$ref":"step"}]},
               {"$ref":"scenario"}]}}
            """));
        await Assert.That(report.Scenarios).Count().IsEqualTo(2);
        await Assert.That(report.Scenarios[1].Steps).Count().IsEqualTo(2);
        await Assert.That(report.Scenarios[1].Steps[1].Exception!.Message).IsEqualTo("failed");
        await Assert.That(report.Scenarios[0].Exception).IsSameReferenceAs(report.Scenarios[0].Steps[0].Exception);
        await Assert.That(report.CustomProperties["$ref"]).IsEqualTo("value");
        await Assert.That(report.CustomProperties["$values"]).IsEqualTo("other");
    }

    [Test]
    [Arguments("{\"schemaVersion\":99}")]
    [Arguments("{\"schemaVersion\":null}")]
    [Arguments("{\"schemaVersion\":\"1\"}")]
    [Arguments("{\"schemaVersion\":1.5}")]
    [Arguments("{\"schemaVersion\":1}")]
    [Arguments("[]")]
    [Arguments("{\"Scenarios\":[{\"Status\":5}]}")]
    [Arguments("{\"Scenarios\":[{\"Status\":1.5}]}")]
    [Arguments("{\"Scenarios\":[{\"Status\":1e1000}]}")]
    [Arguments("{\"Scenarios\":[{\"Status\":\"unknown\"}]}")]
    [Arguments("{\"Scenarios\":[{\"Steps\":[{\"StepType\":99}]}]}")]
    [Arguments("{\"$id\":\"root\",\"Scenarios\":[{\"$ref\":\"root\"}]}")]
    [Arguments("{\"$id\":\"root\",\"Scenarios\":[{\"$id\":\"root\"}]}")]
    [Arguments("{\"$id\":\"root\",\"Scenarios\":[{\"$ref\":\"missing\"}]}")]
    public async Task Invalid_reports_fail_as_JSON_errors(string json)
    {
        await Assert.That(() => HostedReportJson.Deserialize(json)).ThrowsExactly<JsonException>();
    }

    [Test]
    public async Task Async_reader_preserves_stream_ownership_and_null_roots()
    {
        await Assert.That(HostedReportJson.Deserialize("null")).IsNull();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        await Assert.That(await HostedReportJson.DeserializeAsync(stream)).IsNull();
        await Assert.That(stream.CanRead).IsTrue();
        stream.Position = 0;
        await Assert.That(async () => await HostedReportJson.DeserializeAsync(stream, new CancellationToken(true)))
            .Throws<OperationCanceledException>();
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task Legacy_reference_expansion_obeys_the_byte_budget_for_both_readers()
    {
        var json = $$"""
            {"$id":"report","Scenarios":[{"$id":"scenario","Output":"{{new string('x', 512)}}"},{"$ref":"scenario"},{"$ref":"scenario"}]}
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        await Assert.That(() => HostedReportJson.Deserialize(json, bytes.Length + 100)).ThrowsExactly<JsonException>();
        using var stream = new MemoryStream(bytes);
        await Assert.That(async () => await HostedReportJson.DeserializeAsync(stream, maximumExpandedSize: bytes.Length + 100))
            .Throws<JsonException>();
        await Assert.That(stream.CanRead).IsTrue();
        await Assert.That(() => HostedReportJson.Deserialize("{}", -1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Razor_host_assembly_has_no_reference_to_the_execution_assembly()
    {
        var references = typeof(BDTest.Razor.Reports.Controllers.BDTestController).Assembly.GetReferencedAssemblies();
        await Assert.That(references.Any(assembly => assembly.Name == "BDTest")).IsFalse();
        await Assert.That(references.Any(assembly => assembly.Name == "BDTest.Contracts")).IsTrue();
    }

    private const string RichLegacyJson = """
        {"$id":"root","id":"report","Version":"arbitrary producer text","Environment":"environment","Tag":"report tag",
         "BranchName":"branch","MachineName":"machine","AttachmentUploadTokenHash":"hash","CustomProperties":{"property":"value"},
         "TestTimer":{"TestsStartedAt":"2026-10-18T12:00:00Z","TestsFinishedAt":"2026-10-18T12:00:05Z"},
         "Scenarios":[{"Guid":"scenario","ReportId":"report","StoryText":"story","ScenarioText":"scenario","Status":1,
           "FrameworkTestId":"framework","FileName":"test.cs","TestStartupInformation":"startup","TearDownOutput":"teardown",
           "CustomHtmlOutputForReport":"<p>html</p>","Output":"output","StartTime":"2026-10-18T12:00:00Z",
           "EndTime":"2026-10-18T12:00:01.2345678Z","TimeTaken":"0.00:00:01:2345678","Timeout":"00:00:03",
           "RetryCount":1,"IsOutline":true,"Tags":["tag"],"CustomTestInformation":["information"],"ExampleValues":{"input":"value"},
           "ExecutionException":{"$id":"exception","TypeName":"System.InvalidOperationException","Message":"failed","StackTrace":"stack",
             "AsString":"exception text","ShortTypeName":"InvalidOperationException","Data":{"key":"value"},"HResult":-1,"Source":"source",
             "InnerExceptions":[{"TypeName":"System.Exception","Message":"inner","TruncationReason":"DepthLimit"}],"IsWrapper":true},
           "Attachments":[{"$id":"attachment","Id":"attachment","Name":"capture.txt","MimeType":"text/plain","Length":42,
             "AttemptNumber":2,"Uri":"attachments/capture.txt"}],
           "Steps":[{"$id":"step","StepType":6,"StepText":"But action","Status":1,"Guid":"step","ReportId":"report","ScenarioId":"scenario",
             "IsRetry":true,"Exception":{"$ref":"exception"},"Output":"step output","StartTime":"2026-10-18T12:00:00Z",
             "EndTime":"2026-10-18T12:00:01.2345678Z","TimeTaken":"0.00:00:01:2345678","Timeout":"00:00:02","TimedOut":true,
             "Diagnostics":["diagnostic"],"Arguments":[{"$type":"table","Headers":["header"],"Rows":[["cell"]]},
               {"$type":"docString","Content":"body","ContentType":"text/plain"}],"Attachments":[{"$ref":"attachment"}],
             "SkipReason":{"Source":"Rule","AttributeType":"My.SkipAttribute","RuleIndex":3}}],
           "Attempts":[{"Number":2,"Status":1,"Exception":{"$ref":"exception"},"StartTime":"2026-10-18T12:00:00Z",
             "EndTime":"2026-10-18T12:00:01.2345678Z","TimeTaken":"0.00:00:01:2345678","Steps":[{"$ref":"step"}]}],
           "Examples":[{"Guid":"example","ReportId":"report","StoryText":"story","ScenarioText":"example","Status":0,
             "ExampleValues":{"input":"row"},"OutlineId":"scenario"}]}],
         "NotRun":[{"ScenarioId":"unrun","ReportId":"report","StoryText":"unrun story","ScenarioText":"unrun scenario","TestId":"test",
           "CallerMember":"member","CallerFile":"file.cs","Parameters":["parameter"],"Tags":["pending"],
           "CustomTestInformation":["pending information"],"Timeout":"00:00:04",
           "Steps":[{"StepType":"AndThen","StepText":"And pending","Timeout":"00:00:01"}]}]}
        """;
}
