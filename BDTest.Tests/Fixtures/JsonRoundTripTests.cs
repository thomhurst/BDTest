using BDTest.Output;
using BDTest.Test;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class JsonRoundTripTests
{
    [TestCase(StepType.Given, "Given")]
    [TestCase(StepType.AndGiven, "And")]
    [TestCase(StepType.When, "When")]
    [TestCase(StepType.Then, "Then")]
    [TestCase(StepType.AndThen, "And")]
    [TestCase(StepType.ButGiven, "But")]
    [TestCase(StepType.ButWhen, "But")]
    [TestCase(StepType.ButThen, "But")]
    public void Step_round_trip_preserves_type_and_reporting_prefix(StepType stepType, string prefix)
    {
        var step = new Step(new Runnable(() => NoOp()), stepType, "scenario", "report");
        var restored = JsonConvert.DeserializeObject<Step>(JsonConvert.SerializeObject(step));

        Assert.That((int)JObject.Parse(JsonConvert.SerializeObject(restored))["StepType"], Is.EqualTo((int)stepType));
        Assert.That(restored.StepText, Is.EqualTo(step.StepText));

        // Regenerate text so that the stored StepText cannot hide a lost StepType.
        restored.OverriddenStepText = () => "restored text";
        restored.SetStepText();
        Assert.That(restored.StepText, Is.EqualTo($"{prefix} restored text"));
    }

    [Test]
    public void Legacy_step_without_type_defaults_to_given()
    {
        var restored = JsonConvert.DeserializeObject<Step>("{}");
        restored.OverriddenStepText = () => "legacy text";
        restored.SetStepText();
        Assert.That(restored.StepText, Is.EqualTo("Given legacy text"));
    }

    [TestCase(0L, "0.00:00:00:0000000")]
    [TestCase(1L, "0.00:00:00:0000001")]
    [TestCase(937845678901L, "1.02:03:04:5678901")]
    [TestCase(long.MaxValue, "10675199.02:48:05:4775807")]
    public void Duration_round_trip_preserves_ticks_and_wire_format(long ticks, string wireValue)
    {
        var duration = TimeSpan.FromTicks(ticks);
        var converter = new TimespanConverter();
        var json = JsonConvert.SerializeObject(duration, converter);

        Assert.That(json, Is.EqualTo(JsonConvert.SerializeObject(wireValue)));
        Assert.That(JsonConvert.DeserializeObject<TimeSpan>(json, converter), Is.EqualTo(duration));

        var step = JsonConvert.DeserializeObject<Step>($"{{\"TimeTaken\":{json}}}");
        Assert.That(step.TimeTaken, Is.EqualTo(duration));
        Assert.That((string)JObject.Parse(JsonConvert.SerializeObject(step))["TimeTaken"], Is.EqualTo(wireValue));
    }

    [TestCase("\"not a duration\"")]
    [TestCase("\"\"")]
    [TestCase("\"00:00:01\"")]
    [TestCase("\"0.24:00:00:0000000\"")]
    [TestCase("\"10675199.02:48:05:4775808\"")]
    [TestCase("null")]
    [TestCase("123")]
    [TestCase("1.5")]
    [TestCase("true")]
    [TestCase("[]")]
    [TestCase("{}")]
    public void Invalid_or_null_duration_throws_serialization_exception(string json)
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<TimeSpan>(json, new TimespanConverter()));
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Step>($"{{\"TimeTaken\":{json}}}"));
    }

    private static void NoOp() { }
}
