using System.Text.Json;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using BDTest.Test.Steps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportSerializationTests
{
    [Test]
    public void Generated_metadata_round_trips_all_report_types_without_references()
    {
        var started = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromTicks(12345678);
        var information = new[] { new TestInformationAttribute("information") };
        var scenario = new Scenario
        {
            Guid = "outline", ReportId = "report", FrameworkTestId = "framework", FileName = "file.cs",
            StartTime = started, EndTime = started.Add(duration), TimeTaken = duration,
            Status = Status.Failed, RetryCount = 1, IsOutline = true, Timeout = TimeSpan.FromSeconds(3),
            StoryText = new StoryText("story"), ScenarioText = new ScenarioText("scenario"),
            CustomTestInformation = information, Tags = ["tag"], Output = "output",
            TestStartupInformation = "startup", TearDownOutput = "teardown", CustomHtmlOutputForReport = "<p>html</p>",
            ExecutionException = new ExceptionWrapper(new InvalidOperationException("execution failure")),
            Attempts = [new ScenarioAttempt(1, Status.Failed, new Exception("attempt failure"), started, started.Add(duration), duration)],
            Steps = Enum.GetValues<StepType>().Select(type => new Step(type)
            {
                Guid = type.ToString(), ScenarioId = "outline", ReportId = "report", StepText = type + " text",
                StartTime = started, EndTime = started.Add(duration), TimeTaken = duration, Status = Status.Failed,
                Exception = new ExceptionWrapper(new Exception("step failure")), Output = "step output",
                Timeout = TimeSpan.FromSeconds(2), TimedOut = true, Diagnostics = ["render warning"]
            }).ToList(),
            Examples = [new Scenario { Guid = "example", OutlineId = "outline", ExampleValues = new() { ["value"] = "row" }, CustomTestInformation = information }]
        };
        var report = new BDTestOutputModel
        {
            Id = "report", Environment = "environment", Tag = "tag", BranchName = "branch", MachineName = "machine", Version = "version",
            TestTimer = new TestTimer { TestsStartedAt = started, TestsFinishedAt = started.Add(duration) },
            Scenarios = [scenario], CustomProperties = new() { ["property"] = "value" },
            NotRun = [new ScenarioDefinitionSummary
            {
                StoryText = new StoryText("unrun story"), ScenarioText = new ScenarioText("unrun scenario"),
                ScenarioId = "unrun", ReportId = "report", ParameterNames = ["parameter"], CustomTestInformation = information,
                Tags = ["unrun tag"], CallerMember = "member", CallerFile = "unrun.cs", TestId = "test"
            }]
        };

        var json = BDTestJsonHelper.Serialize(report);
        var restored = Required(JsonSerializer.Deserialize(json, BDTestJsonContext.Default.BDTestOutputModel));

        Assert.That(json, Does.Not.Contain("\"$id\"").And.Not.Contain("\"$ref\""));
        Assert.That(json, Does.Contain("\"Status\":\"Failed\"").And.Contain("0.00:00:01:2345678"));
        Assert.That(json, Does.Contain("\"Parameters\":[\"parameter\"]").And.Not.Contain("\"ParameterNames\""));
        Assert.That(BDTestJsonHelper.Serialize(restored), Is.EqualTo(json));
        var actual = Required(restored.Scenarios).Single();
        Assert.That(actual.GetStoryText(), Is.EqualTo("story"));
        Assert.That(actual.GetScenarioText(), Is.EqualTo("scenario"));
        Assert.That(Required(actual.ExecutionException).Message, Is.EqualTo("execution failure"));
        Assert.That(Required(actual.Steps).Select(step => step.StepType), Is.EqualTo(Enum.GetValues<StepType>()));
        Assert.That(actual.Steps.All(step => step.TimeTaken == duration && step.TimedOut), Is.True);
        Assert.That(actual.Steps.All(step => Required(step.Diagnostics).SequenceEqual(new[] { "render warning" })), Is.True);
        Assert.That(Required(Required(actual.Attempts).Single().Exception).Message, Is.EqualTo("attempt failure"));
        Assert.That(Required(actual.Examples).Single().OutlineId, Is.EqualTo(actual.Guid));
        Assert.That(Required(actual.Examples.Single().CustomTestInformation).Single().Information, Is.EqualTo("information"));
        Assert.That(Required(restored.NotRun).Single().GetStoryText(), Is.EqualTo("unrun story"));
        Assert.That(restored.NotRun.Single().ParameterNames, Is.EqualTo(new[] { "parameter" }));
        Assert.That(Required(restored.CustomProperties)["property"], Is.EqualTo("value"));

        var summary = JsonSerializer.Deserialize("""
            {"RecordId":"report","Counts":{"Total":1,"Failed":1},"Status":1,"Tag":"tag"}
            """, ReportJsonContext.Default.TestRunSummary);
        var summaryJson = JsonSerializer.Serialize(summary, ReportJsonContext.Default.TestRunSummary);
        var restoredSummary = Required(JsonSerializer.Deserialize(summaryJson, ReportJsonContext.Default.TestRunSummary));
        Assert.That(restoredSummary.RecordId, Is.EqualTo("report"));
        Assert.That(Required(restoredSummary.Counts).Failed, Is.EqualTo(1));
        Assert.That(JsonSerializer.Serialize(restoredSummary, ReportJsonContext.Default.TestRunSummary), Is.EqualTo(summaryJson));
    }

    [Test]
    public void Legacy_references_and_collection_wrappers_become_plain_values()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            {"$id":"report","id":"legacy","Scenarios":{"$id":"scenarios","$values":[
              {"$id":"scenario","Guid":"scenario","StoryText":"story","ScenarioText":"scenario",
               "Steps":[{"$id":"step","StepType":6,"StepText":"But action","Timeout":"00:00:03","Status":"Passed","TimeTaken":"0.00:00:01:2345678"},{"$ref":"step"}]},
              {"$ref":"scenario"}]},"NotRun":[]}
            """));

        Assert.That(report.Scenarios, Has.Count.EqualTo(2));
        Assert.That(Required(report.Scenarios)[1].GetStoryText(), Is.EqualTo("story"));
        Assert.That(Required(report.Scenarios[1].Steps)[1].StepType, Is.EqualTo(StepType.ButWhen));
        Assert.That(Required(report.Scenarios[1].Steps)[1].Timeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
        Assert.That(Required(report.Scenarios[1].Steps)[1].TimeTaken.Ticks, Is.EqualTo(12345678));
        Assert.That(BDTestJsonHelper.Serialize(report), Does.Not.Contain("\"$id\"").And.Not.Contain("\"$ref\""));
    }

    [TestCase("\"$values\":\"custom value\"")]
    [TestCase("\"$ref\":\"custom reference\"")]
    [TestCase("\"$values\":\"custom value\",\"$ref\":\"custom reference\",\"ordinary\":\"retained\"")]
    public void Legacy_metadata_named_dictionary_keys_are_preserved(string properties)
    {
        var report = Required(BDTestJsonHelper.Deserialize($$$"""
            {"$id":"report","CustomProperties":{"$id":"properties",{{{properties}}}},
             "Scenarios":[{"ExampleValues":{"$id":"examples",{{{properties}}}}}]}
            """));

        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>("{" + properties + "}");
        Assert.That(report.CustomProperties, Is.EquivalentTo(Required(expected)));
        Assert.That(Required(report.Scenarios).Single().ExampleValues, Is.EquivalentTo(Required(expected)));
    }

    [TestCase(0, Status.Passed)]
    [TestCase(1, Status.Failed)]
    [TestCase(2, Status.Inconclusive)]
    [TestCase(3, Status.NotImplemented)]
    [TestCase(4, Status.Skipped)]
    public void Legacy_numeric_statuses_keep_their_meaning_in_scenarios_steps_and_attempts(int value, Status expected)
    {
        var report = Required(BDTestJsonHelper.Deserialize($$"""
            {"$id":"report","Scenarios":[{"Status":{{value}},"Steps":[{"Status":{{value}}}],"Attempts":[{"Status":{{value}}}]}]}
            """));

        var scenario = Required(report.Scenarios).Single();
        Assert.That(scenario.Status, Is.EqualTo(expected));
        Assert.That(Required(scenario.Steps).Single().Status, Is.EqualTo(expected));
        Assert.That(Required(scenario.Attempts).Single().Status, Is.EqualTo(expected));
        Assert.That(BDTestJsonHelper.Serialize(report), Does.Contain($"\"Status\":\"{expected}\""));
    }

    [TestCase("5")]
    [TestCase("1.5")]
    [TestCase("1e1000")]
    public void Invalid_numeric_statuses_fail_with_a_JSON_error(string value)
    {
        Assert.Throws<JsonException>(() => BDTestJsonHelper.Deserialize($$"""
            {"Scenarios":[{"Status":{{value}}}]}
            """));
    }

    [TestCase("{\"$id\":\"report\",\"Scenarios\":[{\"$ref\":\"missing\"}]}")]
    [TestCase("{\"$id\":\"report\",\"Scenarios\":[{\"$ref\":\"report\"}]}")]
    [TestCase("{\"$id\":\"report\",\"Scenarios\":[{\"$id\":\"report\"}]}")]
    public void Invalid_legacy_references_fail_with_a_JSON_error(string json)
    {
        Assert.Throws<JsonException>(() => BDTestJsonHelper.Deserialize(json));
    }

    [Test]
    public async Task Legacy_reference_expansion_obeys_the_upload_byte_budget()
    {
        var json = $$"""
            {"$id":"report","Scenarios":[{"$id":"scenario","Output":"{{new string('x', 512)}}"},{"$ref":"scenario"},{"$ref":"scenario"}]}
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        Assert.Throws<JsonException>(() => BDTestJsonHelper.Deserialize(json, maximumExpandedSize: bytes.Length + 100));
        using var stream = new MemoryStream(bytes);
        await Assert.CatchAsync<JsonException>(async () =>
            await BDTestJsonHelper.DeserializeAsync(stream, maximumExpandedSize: bytes.Length + 100));
        Assert.That(stream.CanRead, Is.True, "The caller owns the input stream.");
    }

    [Test]
    public void Legacy_expansion_has_a_finite_default_budget()
    {
        var references = string.Join(",", Enumerable.Repeat("{\"$ref\":\"scenario\"}", 100));
        var json = $$"""
            {"$id":"report","Scenarios":[{"$id":"scenario","Output":"{{new string('x', 1024 * 1024)}}"},{{references}}]}
            """;

        Assert.Throws<JsonException>(() => BDTestJsonHelper.Deserialize(json));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Registration_preserves_host_MVC_formatters_and_JSON_options(bool useLegacyRegistration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var mvc = services.AddControllersWithViews().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.WriteIndented = true;
        });
        using var before = services.BuildServiceProvider();
        var expected = before.GetRequiredService<IOptions<MvcOptions>>().Value;
        if (useLegacyRegistration)
        {
            var register = Required(typeof(BDTestExtensions).GetMethod("AddBdTestReportControllersAndViews"))
                .CreateDelegate<Func<IMvcBuilder, IMvcBuilder>>();
            Assert.That(register(mvc), Is.SameAs(mvc));
        }
        else
        {
            services.AddBDTestReports();
        }
        using var after = services.BuildServiceProvider();
        Assert.That(after.GetRequiredService<BDTest.Razor.Reports.Interfaces.IDataRepository>(),
            Is.TypeOf<BDTest.Razor.Reports.Implementations.DataRepository>());
        Assert.That(mvc.PartManager.ApplicationParts.Select(part => part.Name),
            Does.Contain(typeof(BDTest.Razor.Reports.Controllers.BDTestController).Assembly.GetName().Name));
        var actual = after.GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.That(actual.InputFormatters.Select(formatter => formatter.GetType()),
            Is.EqualTo(expected.InputFormatters.Select(formatter => formatter.GetType())));
        Assert.That(actual.OutputFormatters.Select(formatter => formatter.GetType()),
            Is.EqualTo(expected.OutputFormatters.Select(formatter => formatter.GetType())));
        var options = after.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        Assert.That(options.PropertyNamingPolicy, Is.SameAs(JsonNamingPolicy.SnakeCaseLower));
        Assert.That(options.WriteIndented, Is.True);
    }
}
