using System.Globalization;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.TestUtilities;

internal static class HistoricalReportFixture
{
    public static string ReadJson() => File.ReadAllText(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "JsonData-v2.0.60.json"));

    public static void AssertCompatible(BDTestOutputModel report)
    {
        var expected = JObject.Parse(ReadJson());
        Assert.That(report, Is.Not.Null);
        Assert.That(report.Id, Is.EqualTo("1113e3bf269c489187245c4339dfd946"));
        Assert.That(report.Version, Is.EqualTo("1.0.0.0"));
        Assert.That(report.MachineName, Is.EqualTo("ASAZ-5CG1039PV2"));
        Assert.That(report.TestTimer.TestsStartedAt, Is.EqualTo(expected["TestTimer"].Value<DateTime>("TestsStartedAt")));
        Assert.That(report.TestTimer.TestsFinishedAt, Is.EqualTo(expected["TestTimer"].Value<DateTime>("TestsFinishedAt")));
        Assert.That(report.Scenarios, Has.Count.EqualTo(13));
        Assert.That(report.Scenarios.Count(scenario => scenario.Status == Status.Passed), Is.EqualTo(3));
        Assert.That(report.Scenarios.Count(scenario => scenario.Status == Status.Failed), Is.EqualTo(6));
        Assert.That(report.Scenarios.Count(scenario => scenario.Status == Status.Inconclusive), Is.EqualTo(4));
        Assert.That(report.NotRun, Is.Empty);
        Assert.That(report.CustomProperties, Is.Empty);

        // Compare the historical contract, excluding reference metadata and runtime-only members.
        foreach (var json in expected["Scenarios"])
        {
            var scenario = report.Scenarios.Single(item => item.Guid == json.Value<string>("Guid"));
            Assert.That(scenario.GetScenarioText(), Is.EqualTo(json.Value<string>("ScenarioText")));
            Assert.That(scenario.GetStoryText(), Is.EqualTo(json.Value<string>("StoryText")));
            Assert.That(scenario.FrameworkTestId, Is.EqualTo(json.Value<string>("FrameworkTestId")));
            Assert.That(scenario.FileName, Is.EqualTo(json.Value<string>("FileName")));
            Assert.That(scenario.Status.ToString(), Is.EqualTo(json.Value<string>("Status")));
            Assert.That(scenario.StartTime, Is.EqualTo(json.Value<DateTime>("StartTime")));
            Assert.That(scenario.EndTime, Is.EqualTo(json.Value<DateTime>("EndTime")));
            Assert.That(scenario.TimeTaken, Is.EqualTo(Duration(json["TimeTaken"])));
            Assert.That(scenario.Output, Is.EqualTo(json.Value<string>("Output")));
            Assert.That(scenario.TestStartupInformation, Is.EqualTo(json.Value<string>("TestStartupInformation")));
            Assert.That(scenario.TearDownOutput, Is.EqualTo(json.Value<string>("TearDownOutput")));
            Assert.That(scenario.CustomHtmlOutputForReport, Is.EqualTo(json.Value<string>("CustomHtmlOutputForReport")));
            Assert.That(scenario.CustomTestInformation.Select(info => info.Information),
                Is.EqualTo(json["CustomTestInformation"].Values<string>()));
            Assert.That(scenario.RetryCount, Is.EqualTo(json.Value<int>("RetryCount")));
            Assert.That(scenario.Steps, Has.Count.EqualTo(json["Steps"].Count()));

            for (var index = 0; index < scenario.Steps.Count; index++)
            {
                var step = scenario.Steps[index];
                var stepJson = json["Steps"][index];
                Assert.That(step.Guid, Is.EqualTo(stepJson.Value<string>("Guid")));
                Assert.That(step.StepText, Is.EqualTo(stepJson.Value<string>("StepText")));
                Assert.That(step.Status.ToString(), Is.EqualTo(stepJson.Value<string>("Status")));
                Assert.That(step.StartTime, Is.EqualTo(stepJson.Value<DateTime>("StartTime")));
                Assert.That(step.EndTime, Is.EqualTo(stepJson.Value<DateTime>("EndTime")));
                Assert.That(step.TimeTaken, Is.EqualTo(Duration(stepJson["TimeTaken"])));
                Assert.That(step.Output, Is.EqualTo(stepJson.Value<string>("Output")));
                Assert.That(step.Exception?.TypeName, Is.EqualTo((string)stepJson.SelectToken("Exception.TypeName")));
                Assert.That(step.Exception?.Message, Is.EqualTo((string)stepJson.SelectToken("Exception.Message")));
                Assert.That(step.Exception?.AsString, Is.EqualTo((string)stepJson.SelectToken("Exception.AsString")));
                Assert.That(step.Timeout, Is.Null);
                Assert.That(step.TimedOut, Is.False);
            }

            // These fields did not exist in the snapshot and must retain safe defaults.
            Assert.That(scenario.Tags, Is.Empty);
            Assert.That(scenario.Attempts, Is.Empty);
            Assert.That(scenario.Examples, Is.Empty);
            Assert.That(scenario.ExampleValues, Is.Empty);
            Assert.That(scenario.IsOutline, Is.False);
            Assert.That(scenario.Timeout, Is.Null);
        }
    }

    private static TimeSpan Duration(JToken value) =>
        TimeSpan.ParseExact((string)value, TimespanConverter.TimeSpanFormatString, CultureInfo.InvariantCulture);
}
