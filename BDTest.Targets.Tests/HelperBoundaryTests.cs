using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Targets.Tests;

public class HelperBoundaryTests
{
    [TestCase("BDTest.Helpers.StepTextHelper")]
    [TestCase("BDTest.Helpers.StoryTextHelper")]
    [TestCase("BDTest.Helpers.BDTestVersionHelper")]
    [TestCase("BDTest.Helpers.TypeHelper")]
    [TestCase("BDTest.Output.TimespanConverter")]
    [TestCase("BDTest.Output.StatusConverter")]
    [TestCase("BDTest.Helpers.JsonConverters.ScenarioTextConverter")]
    [TestCase("BDTest.Helpers.JsonConverters.StoryTextConverter")]
    [TestCase("BDTest.Helpers.JsonConverters.TestInformationAttributeJsonConverter")]
    [TestCase("BDTest.Constants.DefaultValues")]
    [TestCase("BDTest.Constants.DefaultValues+ReportServer")]
    public void Implementation_helpers_are_not_exported(string name)
    {
        Assert.That(typeof(BDTestRun).Assembly.GetExportedTypes().Select(type => type.FullName),
            Does.Not.Contain(name));
    }

    [Test]
    public void Core_only_grants_friend_access_to_the_signed_core_tests()
    {
        var friends = typeof(BDTestRun).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => new AssemblyName(attribute.AssemblyName)).ToArray();
        Assert.That(friends.Select(friend => friend.Name), Is.EqualTo(new[] { "BDTest.Tests" }));
        Assert.That(friends[0].GetPublicKey(), Is.Not.Empty);
    }

    [TestCase(0, BDTest.Test.Status.Passed)]
    [TestCase(1, BDTest.Test.Status.Failed)]
    [TestCase(2, BDTest.Test.Status.Inconclusive)]
    [TestCase(3, BDTest.Test.Status.NotImplemented)]
    [TestCase(4, BDTest.Test.Status.Skipped)]
    public void Standalone_status_metadata_preserves_the_historical_mapping(int legacy, BDTest.Test.Status expected)
    {
        var metadata = BDTestJsonContext.Default.Status;
        Assert.That(System.Text.Json.JsonSerializer.Deserialize(legacy.ToString(), metadata), Is.EqualTo(expected));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(expected, metadata), Is.EqualTo("\"" + expected + "\""));
    }

    [Test]
    public void Public_generated_metadata_handles_private_converters_without_reflection()
    {
        const string json = """
            {"Scenarios":[{"ScenarioText":"a scenario","StoryText":"a story","Status":"Passed",
            "Steps":[{"TimeTaken":"1.02:03:04:1234567"}],"CustomTestInformation":["information"]}]}
            """;
        var options = new System.Text.Json.JsonSerializerOptions
        {
            TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine()
        };
        var context = new BDTestJsonContext(options);
        var report = System.Text.Json.JsonSerializer.Deserialize(json, context.BDTestOutputModel)!;
        var scenario = report.Scenarios!.Single();
        Assert.That(scenario.GetScenarioText(), Is.EqualTo("a scenario"));
        Assert.That(scenario.GetStoryText(), Is.EqualTo("a story"));
        Assert.That(scenario.Status, Is.EqualTo(BDTest.Test.Status.Passed));
        Assert.That(scenario.Steps!.Single().TimeTaken,
            Is.EqualTo(new TimeSpan(1, 2, 3, 4).Add(TimeSpan.FromTicks(1234567))));
        var serialized = System.Text.Json.JsonSerializer.Serialize(report, context.BDTestOutputModel);
        Assert.That(serialized, Does.Contain("1.02:03:04:1234567").And.Contain("information"));
        Assert.That(BDTestJsonHelper.Deserialize(serialized)!.Scenarios!.Single().GetStoryText(),
            Is.EqualTo("a story"));
    }
}
