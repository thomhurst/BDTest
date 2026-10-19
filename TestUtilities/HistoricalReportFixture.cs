using System.Globalization;
using System.Runtime.CompilerServices;
using BDTest.Maps;
using BDTest.Test;
using Newtonsoft.Json.Linq;

namespace BDTest.TestUtilities;

internal static class HistoricalReportFixture
{
    public static string ReadJson() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Data", "JsonData-v2.0.60.json"));

    public static void AssertCompatible(BDTestOutputModel report)
    {
        var expected = JObject.Parse(ReadJson());
        ArgumentNullException.ThrowIfNull(report);
        Equal(report.Id, "1113e3bf269c489187245c4339dfd946");
        Equal(report.Version, "1.0.0.0");
        Equal(report.MachineName, "ASAZ-5CG1039PV2");
        var timer = report.TestTimer ?? throw new InvalidDataException("Missing historical test timer.");
        var scenarios = report.Scenarios ?? throw new InvalidDataException("Missing historical scenarios.");
        Equal(timer.TestsStartedAt, RequiredProperty(expected, "TestTimer").Value<DateTime>("TestsStartedAt"));
        Equal(timer.TestsFinishedAt, RequiredProperty(expected, "TestTimer").Value<DateTime>("TestsFinishedAt"));
        Equal(scenarios.Count, 13);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Passed), 3);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Failed), 6);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Inconclusive), 4);
        Equal(report.NotRun?.Any(), false);
        Equal(report.CustomProperties?.Any(), false);

        // Compare the historical contract, excluding reference metadata and runtime-only members.
        foreach (var json in RequiredProperty(expected, "Scenarios"))
        {
            var scenario = scenarios.Single(item => item.Guid == json.Value<string>("Guid"));
            Equal(scenario.GetScenarioText(), json.Value<string>("ScenarioText"));
            Equal(scenario.GetStoryText(), json.Value<string>("StoryText"));
            Equal(scenario.FrameworkTestId, json.Value<string>("FrameworkTestId"));
            Equal(scenario.FileName, json.Value<string>("FileName"));
            Equal(scenario.Status.ToString(), json.Value<string>("Status"));
            Equal(scenario.StartTime, json.Value<DateTime>("StartTime"));
            Equal(scenario.EndTime, json.Value<DateTime>("EndTime"));
            Equal(scenario.TimeTaken, Duration(json["TimeTaken"]));
            Equal(scenario.Output, json.Value<string>("Output"));
            Equal(scenario.TestStartupInformation, json.Value<string>("TestStartupInformation"));
            Equal(scenario.TearDownOutput, json.Value<string>("TearDownOutput"));
            Equal(scenario.CustomHtmlOutputForReport, json.Value<string>("CustomHtmlOutputForReport"));
            SequenceEqual((scenario.CustomTestInformation ?? throw new InvalidDataException("Missing historical test information.")).Select(info => info.Information),
                RequiredProperty(json, "CustomTestInformation").Values<string>());
            Equal(scenario.RetryCount, json.Value<int>("RetryCount"));
            var steps = scenario.Steps ?? throw new InvalidDataException("Missing historical steps.");
            Equal(steps.Count, RequiredProperty(json, "Steps").Count());

            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                var stepJson = RequiredProperty(json, "Steps")[index]
                    ?? throw new InvalidDataException($"Missing historical step {index}.");
                Equal(step.Guid, stepJson.Value<string>("Guid"));
                Equal(step.StepText, stepJson.Value<string>("StepText"));
                Equal(step.Status.ToString(), stepJson.Value<string>("Status"));
                Equal(step.StartTime, stepJson.Value<DateTime>("StartTime"));
                Equal(step.EndTime, stepJson.Value<DateTime>("EndTime"));
                Equal(step.TimeTaken, Duration(stepJson["TimeTaken"]));
                Equal(step.Output, stepJson.Value<string>("Output"));
                Equal(step.Exception?.TypeName, (string?)stepJson.SelectToken("Exception.TypeName"));
                Equal(step.Exception?.Message, (string?)stepJson.SelectToken("Exception.Message"));
                Equal(step.Exception?.AsString, (string?)stepJson.SelectToken("Exception.AsString"));
                Equal(step.Timeout, null);
                Equal(step.TimedOut, false);
            }

            // These fields did not exist in the snapshot and must retain safe defaults.
            Equal(scenario.Tags?.Any(), false);
            Equal(scenario.Attempts?.Any(), false);
            Equal(scenario.Examples?.Any(), false);
            Equal(scenario.ExampleValues?.Any(), false);
            Equal(scenario.IsOutline, false);
            Equal(scenario.Timeout, null);
        }
    }

    // Shared by the NUnit core and TUnit host suites; failures remain ordinary test failures.
    public static void AssertCompatible(BDTest.Contracts.BDTestReport report)
    {
        var expected = JObject.Parse(ReadJson());
        ArgumentNullException.ThrowIfNull(report);
        Equal(report.Id, "1113e3bf269c489187245c4339dfd946");
        Equal(report.ProducerVersion, "1.0.0.0");
        Equal(report.MachineName, "ASAZ-5CG1039PV2");
        var timer = report.TestTimer ?? throw new InvalidDataException("Missing historical test timer.");
        var scenarios = report.Scenarios ?? throw new InvalidDataException("Missing historical scenarios.");
        Equal(timer.TestsStartedAt.DateTime, RequiredProperty(expected, "TestTimer").Value<DateTime>("TestsStartedAt"));
        Equal(timer.TestsFinishedAt.DateTime, RequiredProperty(expected, "TestTimer").Value<DateTime>("TestsFinishedAt"));
        Equal(scenarios.Count, 13);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Passed), 3);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Failed), 6);
        Equal(scenarios.Count(scenario => scenario.Status == Status.Inconclusive), 4);
        Equal(report.NotRun?.Any(), false);
        Equal(report.CustomProperties?.Any(), false);

        // Compare the historical contract, excluding reference metadata and runtime-only members.
        foreach (var json in RequiredProperty(expected, "Scenarios"))
        {
            var scenario = scenarios.Single(item => item.Guid == json.Value<string>("Guid"));
            Equal(scenario.ScenarioText, json.Value<string>("ScenarioText"));
            Equal(scenario.StoryText, json.Value<string>("StoryText"));
            Equal(scenario.FrameworkTestId, json.Value<string>("FrameworkTestId"));
            Equal(scenario.FileName, json.Value<string>("FileName"));
            Equal(scenario.Status.ToString(), json.Value<string>("Status"));
            Equal(scenario.StartTime.DateTime, json.Value<DateTime>("StartTime"));
            Equal(scenario.EndTime.DateTime, json.Value<DateTime>("EndTime"));
            Equal(scenario.TimeTaken, Duration(json["TimeTaken"]));
            Equal(scenario.Output, json.Value<string>("Output"));
            Equal(scenario.TestStartupInformation, json.Value<string>("TestStartupInformation"));
            Equal(scenario.TearDownOutput, json.Value<string>("TearDownOutput"));
            Equal(scenario.CustomHtmlOutputForReport, json.Value<string>("CustomHtmlOutputForReport"));
            SequenceEqual((scenario.CustomTestInformation ?? throw new InvalidDataException("Missing historical test information.")),
                RequiredProperty(json, "CustomTestInformation").Values<string>());
            Equal(scenario.RetryCount, json.Value<int>("RetryCount"));
            var steps = scenario.Steps ?? throw new InvalidDataException("Missing historical steps.");
            Equal(steps.Count, RequiredProperty(json, "Steps").Count());

            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                var stepJson = RequiredProperty(json, "Steps")[index]
                    ?? throw new InvalidDataException($"Missing historical step {index}.");
                Equal(step.Guid, stepJson.Value<string>("Guid"));
                Equal(step.StepText, stepJson.Value<string>("StepText"));
                Equal(step.Status.ToString(), stepJson.Value<string>("Status"));
                Equal(step.StartTime.DateTime, stepJson.Value<DateTime>("StartTime"));
                Equal(step.EndTime.DateTime, stepJson.Value<DateTime>("EndTime"));
                Equal(step.TimeTaken, Duration(stepJson["TimeTaken"]));
                Equal(step.Output, stepJson.Value<string>("Output"));
                Equal(step.Exception?.TypeName, (string?)stepJson.SelectToken("Exception.TypeName"));
                Equal(step.Exception?.Message, (string?)stepJson.SelectToken("Exception.Message"));
                Equal(step.Exception?.AsString, (string?)stepJson.SelectToken("Exception.AsString"));
                Equal(step.Timeout, null);
                Equal(step.TimedOut, false);
            }

            // These fields did not exist in the snapshot and must retain safe defaults.
            Equal(scenario.Tags?.Any(), false);
            Equal(scenario.Attempts?.Any(), false);
            Equal(scenario.Examples?.Any(), false);
            Equal(scenario.ExampleValues?.Any(), false);
            Equal(scenario.IsOutline, false);
            Equal(scenario.Timeout, null);
        }
    }

    // Shared by the NUnit core and TUnit host suites; failures remain ordinary test failures.
    private static void Equal<T>(T actual, T expected, [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new InvalidDataException($"{expression}: expected '{expected}', got '{actual}'.");
    }

    private static void SequenceEqual<T>(IEnumerable<T> actual, IEnumerable<T> expected,
        [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"{expression}: historical values differ in content or order.");
    }

    private static JToken RequiredProperty(JToken value, string name) =>
        value[name] ?? throw new InvalidDataException($"Missing historical property {name}.");

    private static TimeSpan Duration(JToken? value) =>
        TimeSpan.ParseExact((string?)value ?? throw new InvalidDataException("Missing historical duration."), @"d\.hh\:mm\:ss\:fffffff", CultureInfo.InvariantCulture);
}
