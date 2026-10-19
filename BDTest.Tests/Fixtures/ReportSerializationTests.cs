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
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportSerializationTests
{
    [Test]
    public async Task Generated_metadata_round_trips_all_report_types_without_references()
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


        await Assert.That(json).DoesNotContain("\"$id\"").And.DoesNotContain("\"$ref\"");

        await Assert.That(json).Contains("\"Status\":\"Failed\"").And.Contains("0.00:00:01:2345678");

        await Assert.That(json).Contains("\"Parameters\":[\"parameter\"]").And.DoesNotContain("\"ParameterNames\"");
        await Assert.That(BDTestJsonHelper.Serialize(restored)).IsEqualTo(json);
        var actual = Required(restored.Scenarios).Single();
        await Assert.That(actual.GetStoryText()).IsEqualTo("story");
        await Assert.That(actual.GetScenarioText()).IsEqualTo("scenario");
        await Assert.That(Required(actual.ExecutionException).Message).IsEqualTo("execution failure");
        await Assert.That(Required(actual.Steps).Select(step => step.StepType)).IsEquivalentTo(Enum.GetValues<StepType>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(actual.Steps.All(step => step.TimeTaken == duration && step.TimedOut)).IsTrue();
        await Assert.That(actual.Steps.All(step => Required(step.Diagnostics).SequenceEqual(new[] { "render warning" }))).IsTrue();
        await Assert.That(Required(Required(actual.Attempts).Single().Exception).Message).IsEqualTo("attempt failure");
        await Assert.That(Required(actual.Examples).Single().OutlineId).IsEqualTo(actual.Guid);
        await Assert.That(Required(actual.Examples.Single().CustomTestInformation).Single().Information).IsEqualTo("information");
        await Assert.That(Required(restored.NotRun).Single().GetStoryText()).IsEqualTo("unrun story");
        await Assert.That(restored.NotRun.Single().ParameterNames).IsEquivalentTo((new[] { "parameter" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(restored.CustomProperties)["property"]).IsEqualTo("value");

        var summary = JsonSerializer.Deserialize("""
            {"RecordId":"report","Counts":{"Total":1,"Failed":1},"Status":1,"Tag":"tag"}
            """, ReportJsonContext.Default.TestRunSummary);
        var summaryJson = JsonSerializer.Serialize(summary, ReportJsonContext.Default.TestRunSummary);
        var restoredSummary = Required(JsonSerializer.Deserialize(summaryJson, ReportJsonContext.Default.TestRunSummary));
        await Assert.That(restoredSummary.RecordId).IsEqualTo("report");
        await Assert.That(Required(restoredSummary.Counts).Failed).IsEqualTo(1);
        await Assert.That(JsonSerializer.Serialize(restoredSummary, ReportJsonContext.Default.TestRunSummary)).IsEqualTo(summaryJson);
    }

    [Test]
    public async Task Legacy_references_and_collection_wrappers_become_plain_values()
    {
        var report = Required(BDTestJsonHelper.Deserialize("""
            {"$id":"report","id":"legacy","Scenarios":{"$id":"scenarios","$values":[
              {"$id":"scenario","Guid":"scenario","StoryText":"story","ScenarioText":"scenario",
               "Steps":[{"$id":"step","StepType":6,"StepText":"But action","Timeout":"00:00:03","Status":"Passed","TimeTaken":"0.00:00:01:2345678"},{"$ref":"step"}]},
              {"$ref":"scenario"}]},"NotRun":[]}
            """));

        await Assert.That(report.Scenarios).Count().IsEqualTo(2);
        await Assert.That(Required(report.Scenarios)[1].GetStoryText()).IsEqualTo("story");
        await Assert.That(Required(report.Scenarios[1].Steps)[1].StepType).IsEqualTo(StepType.ButWhen);
        await Assert.That(Required(report.Scenarios[1].Steps)[1].Timeout).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(Required(report.Scenarios[1].Steps)[1].TimeTaken.Ticks).IsEqualTo(12345678);

        await Assert.That(BDTestJsonHelper.Serialize(report)).DoesNotContain("\"$id\"").And.DoesNotContain("\"$ref\"");
    }

    [Test]
    [Arguments("\"$values\":\"custom value\"")]
    [Arguments("\"$ref\":\"custom reference\"")]
    [Arguments("\"$values\":\"custom value\",\"$ref\":\"custom reference\",\"ordinary\":\"retained\"")]
    public async Task Legacy_metadata_named_dictionary_keys_are_preserved(string properties)
    {
        var report = Required(BDTestJsonHelper.Deserialize($$$"""
            {"$id":"report","CustomProperties":{"$id":"properties",{{{properties}}}},
             "Scenarios":[{"ExampleValues":{"$id":"examples",{{{properties}}}}}]}
            """));

        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>("{" + properties + "}");

        await Assert.That(report.CustomProperties).IsEquivalentTo(Required(expected), global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        await Assert.That(Required(report.Scenarios).Single().ExampleValues).IsEquivalentTo(Required(expected), global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [Arguments(0, Status.Passed)]
    [Arguments(1, Status.Failed)]
    [Arguments(2, Status.Inconclusive)]
    [Arguments(3, Status.NotImplemented)]
    [Arguments(4, Status.Skipped)]
    public async Task Legacy_numeric_statuses_keep_their_meaning_in_scenarios_steps_and_attempts(int value, Status expected)
    {
        var report = Required(BDTestJsonHelper.Deserialize($$"""
            {"$id":"report","Scenarios":[{"Status":{{value}},"Steps":[{"Status":{{value}}}],"Attempts":[{"Status":{{value}}}]}]}
            """));

        var scenario = Required(report.Scenarios).Single();
        await Assert.That(scenario.Status).IsEqualTo(expected);
        await Assert.That(Required(scenario.Steps).Single().Status).IsEqualTo(expected);
        await Assert.That(Required(scenario.Attempts).Single().Status).IsEqualTo(expected);
        await Assert.That(BDTestJsonHelper.Serialize(report)).Contains($"\"Status\":\"{expected}\"");
    }

    [Test]
    [Arguments("5")]
    [Arguments("1.5")]
    [Arguments("1e1000")]
    public async Task Invalid_numeric_statuses_fail_with_a_JSON_error(string value)
    {
        await Assert.That(() => BDTestJsonHelper.Deserialize($$"""
            {"Scenarios":[{"Status":{{value}}}]}
            """)).ThrowsExactly<JsonException>();
    }

    [Test]
    [Arguments("{\"$id\":\"report\",\"Scenarios\":[{\"$ref\":\"missing\"}]}")]
    [Arguments("{\"$id\":\"report\",\"Scenarios\":[{\"$ref\":\"report\"}]}")]
    [Arguments("{\"$id\":\"report\",\"Scenarios\":[{\"$id\":\"report\"}]}")]
    public async Task Invalid_legacy_references_fail_with_a_JSON_error(string json)
    {
        await Assert.That(() => BDTestJsonHelper.Deserialize(json)).ThrowsExactly<JsonException>();
    }

    [Test]
    public async Task Legacy_reference_expansion_obeys_the_upload_byte_budget()
    {
        var json = $$"""
            {"$id":"report","Scenarios":[{"$id":"scenario","Output":"{{new string('x', 512)}}"},{"$ref":"scenario"},{"$ref":"scenario"}]}
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await Assert.That(() => BDTestJsonHelper.Deserialize(json, maximumExpandedSize: bytes.Length + 100)).ThrowsExactly<JsonException>();
        using var stream = new MemoryStream(bytes);
        await Assert.That(async () =>
            await BDTestJsonHelper.DeserializeAsync(stream, maximumExpandedSize: bytes.Length + 100)).Throws<JsonException>();
        await Assert.That(stream.CanRead).IsTrue().Because("The caller owns the input stream.");
    }

    [Test]
    public async Task Legacy_expansion_has_a_finite_default_budget()
    {
        var references = string.Join(",", Enumerable.Repeat("{\"$ref\":\"scenario\"}", 100));
        var json = $$"""
            {"$id":"report","Scenarios":[{"$id":"scenario","Output":"{{new string('x', 1024 * 1024)}}"},{{references}}]}
            """;

        await Assert.That(() => BDTestJsonHelper.Deserialize(json)).ThrowsExactly<JsonException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Registration_preserves_host_MVC_formatters_and_JSON_options(bool useLegacyRegistration)
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
            await Assert.That(register(mvc)).IsSameReferenceAs(mvc);
        }
        else
        {
            services.AddBDTestReports();
        }
        using var after = services.BuildServiceProvider();
        await Assert.That(after.GetRequiredService<BDTest.Razor.Reports.Interfaces.IDataRepository>()).IsTypeOf<BDTest.Razor.Reports.Implementations.DataRepository>();
        await Assert.That(mvc.PartManager.ApplicationParts.Select(part => part.Name)).Contains(Required(typeof(BDTest.Razor.Reports.Controllers.BDTestController).Assembly.GetName().Name));
        var actual = after.GetRequiredService<IOptions<MvcOptions>>().Value;

        await Assert.That(actual.InputFormatters.Select(formatter => formatter.GetType())).IsEquivalentTo(expected.InputFormatters.Select(formatter => formatter.GetType()), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(actual.OutputFormatters.Select(formatter => formatter.GetType())).IsEquivalentTo(expected.OutputFormatters.Select(formatter => formatter.GetType()), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var options = after.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        await Assert.That(options.PropertyNamingPolicy).IsSameReferenceAs(JsonNamingPolicy.SnakeCaseLower);
        await Assert.That(options.WriteIndented).IsTrue();
    }
}
