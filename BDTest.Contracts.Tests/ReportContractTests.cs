using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BDTest.Contracts;
using NUnit.Framework;

namespace BDTest.Contracts.Tests;

public class ReportContractTests
{
    [Test]
    public void Public_data_types_have_no_setters_fields_or_mutable_collection_properties()
    {
        var types = typeof(BDTestReport).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type != typeof(BDTestReportJsonContext));
        foreach (var type in types)
        {
            Assert.That(type.IsSealed, Is.True, type.FullName);
            Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, type.FullName);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.SetMethod, Is.Null, $"{type.Name}.{property.Name}");
                AssertImmutableProperty(property.PropertyType, $"{type.Name}.{property.Name}");
            }
        }
        Assert.That(typeof(BDTestReport).Assembly.GetReferencedAssemblies().Select(name => name.Name),
            Does.Not.Contain("BDTest"));
    }

    private static void AssertImmutableProperty(Type type, string name)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return;
        Assert.That(type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)), Is.True, name);
        foreach (var argument in type.GetGenericArguments()) AssertImmutableProperty(argument, name);
    }

    [Test]
    public void Collections_are_snapshots_including_nested_rows_and_exception_data()
    {
        var headers = new[] { "Name" };
        var cells = new[] { "Alice" };
        var rows = new List<IReadOnlyList<string>> { cells };
        var table = new StepTable(headers, rows);
        var data = new Dictionary<string, string> { ["actual"] = "before" };
        var children = new List<ExceptionInfo> { new("Tests.Inner", "inner") };
        var exception = new ExceptionInfo("Tests.Outer", "outer", data: data, innerExceptions: children);
        var arguments = new List<StepArgument> { table };
        var diagnostics = new List<string> { "diagnostic" };
        var attachments = new List<AttachmentInfo> { new("attachment", "log", "text/plain", 12, 1) };
        var step = new StepResult(StepType.Given, "Given text", Status.Failed,
            arguments: arguments, diagnostics: diagnostics, attachments: attachments, exception: exception);
        var steps = new List<StepResult> { step };
        var attempts = new List<ScenarioAttemptResult> { new(1, Status.Failed, steps: steps) };
        var values = new Dictionary<string, string> { ["name"] = "Alice" };
        var tags = new List<string> { "smoke" };
        var information = new List<string> { "information" };
        var examples = new List<ScenarioResult> { new("row", "report", "story", "row", Status.Passed) };
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Failed,
            steps: steps, attempts: attempts, exampleValues: values, examples: examples, tags: tags,
            customTestInformation: information, attachments: attachments);
        var parameters = new List<string> { "parameter" };
        var summary = new ScenarioDefinitionSummary("pending", "report", "story", "pending",
            parameters: parameters, tags: tags, customTestInformation: information);
        var scenarios = new List<ScenarioResult> { scenario };
        var notRun = new List<ScenarioDefinitionSummary> { summary };
        var properties = new Dictionary<string, string> { ["build"] = "123" };
        var report = new BDTestReport(1, "report", "3.0.0", scenarios, notRun, customProperties: properties);

        headers[0] = cells[0] = "changed";
        rows.Clear(); data.Clear(); children.Clear(); arguments.Clear(); diagnostics.Clear();
        attachments.Clear(); steps.Clear(); attempts.Clear(); values.Clear(); tags.Clear();
        information.Clear(); examples.Clear(); parameters.Clear(); scenarios.Clear(); notRun.Clear(); properties.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(report.Scenarios.Single().Steps.Single().Arguments.Single(), Is.SameAs(table));
            Assert.That(table.Headers, Is.EqualTo(new[] { "Name" }));
            Assert.That(table.Rows.Single(), Is.EqualTo(new[] { "Alice" }));
            Assert.That(exception.Data["actual"], Is.EqualTo("before"));
            Assert.That(exception.InnerExceptions.Single().TypeName, Is.EqualTo("Tests.Inner"));
            Assert.That(step.Diagnostics.Single(), Is.EqualTo("diagnostic"));
            Assert.That(step.Attachments.Single().Id, Is.EqualTo("attachment"));
            Assert.That(scenario.Attachments.Single().Length, Is.EqualTo(12));
            Assert.That(scenario.Attempts.Single().Steps.Single(), Is.SameAs(step));
            Assert.That(scenario.ExampleValues["name"], Is.EqualTo("Alice"));
            Assert.That(scenario.Examples.Single().Guid, Is.EqualTo("row"));
            Assert.That(scenario.Tags.Single(), Is.EqualTo("smoke"));
            Assert.That(scenario.CustomTestInformation.Single(), Is.EqualTo("information"));
            Assert.That(report.NotRun.Single().Parameters.Single(), Is.EqualTo("parameter"));
            Assert.That(summary.Tags.Single(), Is.EqualTo("smoke"));
            Assert.That(summary.CustomTestInformation.Single(), Is.EqualTo("information"));
            Assert.That(report.CustomProperties["build"], Is.EqualTo("123"));
        });
        Assert.Throws<NotSupportedException>(() => ((IList<StepResult>)scenario.Steps).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)table.Rows[0])[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)exception.Data).Clear());
    }

    [Test]
    public void Every_step_type_and_status_round_trips_as_a_name()
    {
        foreach (var type in Enum.GetValues<StepType>())
        foreach (var status in Enum.GetValues<Status>())
        {
            var report = new BDTestReport(1, "report", "3.1.2", new[]
            {
                new ScenarioResult("scenario", "report", "story", "scenario", status,
                    steps: new[] { new StepResult(type, "step", status, isRetry: true) })
            });
            var json = BDTestReportJson.Serialize(report);
            var step = BDTestReportJson.Deserialize(json).Scenarios.Single().Steps.Single();
            Assert.That(step.StepType, Is.EqualTo(type));
            Assert.That(step.Status, Is.EqualTo(status));
            Assert.That(step.IsRetry, Is.True);
            using var document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.GetProperty("scenarios")[0].GetProperty("steps")[0]
                .GetProperty("stepType").GetString(), Is.EqualTo(type.ToString()));
        }
    }

    [Test]
    public void Populated_report_round_trip_preserves_all_fields()
    {
        var start = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(1));
        var exception = new ExceptionInfo(typeof(First.Failure).FullName!, "outer", "stack", "original",
            shortTypeName: "Failure", data: new Dictionary<string, string> { ["expected"] = "42" },
            hResult: 123, source: "source", isWrapper: true, truncationReason: "DepthLimit",
            innerExceptions: new[] { new ExceptionInfo(typeof(Second.Failure).FullName!, "inner") });
        var attachment = new AttachmentInfo("file", "log.txt", "text/plain", 15, 2, "https://example.test/file");
        var step = new StepResult(StepType.ButThen, "But fails", Status.Failed, guid: "step", reportId: "report",
            scenarioId: "scenario", isRetry: true, exception: exception, output: "output", startTime: start,
            endTime: start.AddSeconds(2), timeTaken: TimeSpan.FromSeconds(2), timeout: TimeSpan.FromSeconds(1),
            timedOut: true, diagnostics: new[] { "diagnostic" }, attachments: new[] { attachment },
            arguments: new StepArgument[] { new StepTable(new[] { "Name" }, new[] { new[] { "Alice" } }),
                new StepDocString("{\"x\":1}", "application/json") });
        var row = new ScenarioResult("row", "report", "story", "example", Status.Passed, outlineId: "scenario");
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Failed,
            frameworkTestId: "framework", fileName: "test.cs", testStartupInformation: "setup", tearDownOutput: "teardown",
            customHtmlOutputForReport: "<p>html</p>", output: "output", startTime: start, endTime: start.AddSeconds(3),
            timeTaken: TimeSpan.FromSeconds(3), timeout: TimeSpan.FromSeconds(2), retryCount: 1, isOutline: true,
            steps: new[] { step }, examples: new[] { row }, tags: new[] { "smoke" }, exception: exception,
            attempts: new[] { new ScenarioAttemptResult(1, Status.Failed, exception, start, start.AddSeconds(2),
                TimeSpan.FromSeconds(2), new[] { step }) }, exampleValues: new Dictionary<string, string> { ["n"] = "1" },
            attachments: new[] { attachment }, customTestInformation: new[] { "info" });
        var pending = new ScenarioDefinitionSummary("pending", "report", "story", "pending", testId: "test",
            callerMember: "method", callerFile: "test.cs", parameters: new[] { "n" }, tags: new[] { "pending" },
            customTestInformation: new[] { "info" }, timeout: TimeSpan.FromSeconds(1));
        var report = new BDTestReport(1, "report", "3.1.2+revision", new[] { scenario }, new[] { pending },
            environment: "ci", tag: "nightly", branchName: "main", machineName: "agent",
            testTimer: new ReportTimer(start, start.AddSeconds(3)),
            customProperties: new Dictionary<string, string> { ["build"] = "1" }, attachmentUploadTokenHash: "hash");

        var json = BDTestReportJson.Serialize(report);
        // Keep a schema fixture independent of constructor/property changes in this test.
        var fixture = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "report-schema-v1.json"));
        Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(fixture)), Is.True,
            "Report JSON changed: review schema compatibility and version before updating the fixture.");
        var restored = BDTestReportJson.Deserialize(json);
        Assert.That(BDTestReportJson.Serialize(restored), Is.EqualTo(json));
        Assert.Multiple(() =>
        {
            Assert.That(restored.SchemaVersion, Is.EqualTo(1));
            Assert.That(restored.ProducerVersion, Is.EqualTo("3.1.2+revision"));
            Assert.That(restored.Scenarios.Single().Exception!.TypeName, Is.EqualTo(typeof(First.Failure).FullName));
            Assert.That(restored.Scenarios.Single().Exception!.InnerExceptions.Single().TypeName,
                Is.EqualTo(typeof(Second.Failure).FullName));
            Assert.That(restored.Scenarios.Single().TimedOut, Is.True);
            Assert.That(restored.Scenarios.Single().Steps.Single().Arguments[0], Is.TypeOf<StepTable>());
            Assert.That(restored.Scenarios.Single().Steps.Single().Arguments[1], Is.TypeOf<StepDocString>());
            Assert.That(restored.TestTimer!.ElapsedTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(restored.NotRun.Single().TestId, Is.EqualTo("test"));
        });
    }

    [Test]
    public void Collection_contract_rejects_null_entries_and_invalid_table_rows()
    {
        Assert.Throws<ArgumentException>(() => new BDTestReport(1, "id", "version", new ScenarioResult[] { null! }));
        Assert.Throws<ArgumentException>(() => new ExceptionInfo("Type", "message",
            data: new Dictionary<string, string> { ["key"] = null! }));
        Assert.Throws<ArgumentException>(() => new StepTable(new[] { "Name" }, new[] { Array.Empty<string>() }));
        Assert.Throws<ArgumentException>(() => new StepTable(new[] { "Name" }, new IReadOnlyList<string>[] { null! }));
        var table = new StepTable(new string[] { null! }, new[] { new[] { (string)null! } });
        Assert.That(table.Headers.Single(), Is.Empty);
        Assert.That(table.Rows.Single().Single(), Is.Empty);
    }

    [TestCase("[]")]
    [TestCase("{\"schemaVersion\":null}")]
    [TestCase("{\"schemaVersion\":1.5}")]
    [TestCase("{\"schemaVersion\":2147483648}")]
    [TestCase("{}")]

    [TestCase("null")]
    [TestCase("{\"schemaVersion\":0}")]
    [TestCase("{\"schemaVersion\":2}")]
    [TestCase("{\"schemaVersion\":\"1\"}")]
    public void Current_schema_reader_rejects_missing_or_unsupported_versions(string json)
        => Assert.Throws<JsonException>(() => BDTestReportJson.Deserialize(json));

    [Test]
    public void Missing_optional_collections_are_empty()
    {
        var report = BDTestReportJson.Deserialize("{\"schemaVersion\":1,\"id\":\"report\",\"producerVersion\":\"3.0.0\","
            + "\"scenarios\":[{\"guid\":\"scenario\",\"reportId\":\"report\",\"storyText\":\"story\",\"scenarioText\":\"scenario\",\"status\":\"NotRun\"}]}");
        Assert.That(report.NotRun, Is.Empty);
        Assert.That(report.CustomProperties, Is.Empty);
        Assert.That(report.Scenarios.Single().Steps, Is.Empty);
        Assert.That(report.Scenarios.Single().Examples, Is.Empty);
        Assert.That(report.Scenarios.Single().Attempts, Is.Empty);
    }
}

internal static class First { internal sealed class Failure : Exception; }
internal static class Second { internal sealed class Failure : Exception; }
